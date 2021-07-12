using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiscordMoniesGame
{
    public class Board
    {
        public readonly record struct TitleDeed(int Position, int[] RentValues, int MortgageValue, int HouseCost, int HotelCost);
        public readonly record struct LuckCard(string Description, string Command);

        public int StartingMoney { get; }
        public int JailFine { get; }
        public SpaceBounds JailBounds { get; }
        public ImmutableArray<string> GroupNames { get; }
        public Space[] BoardSpaces { get; }

        public int AvailableHouses { get; set; } = 32;
        public int AvailableHotels { get; set; } = 12;

        public TitleDeed[] TitleDeeds { get; }
        public ConcurrentStack<LuckCard> ChanceCards { get; }
        public ConcurrentBag<LuckCard> UsedChanceCards { get; } = new();
        public ConcurrentStack<LuckCard> ChestCards { get; }
        public ConcurrentBag<LuckCard> UsedChestCards { get; }  = new();

        public int VisitingJailPosition => Array.FindIndex(BoardSpaces, s => s.Name == "Visiting Jail");
        public int PassGoValue => ((GoSpace)Array.Find(BoardSpaces, s => s is GoSpace)!).Value;

        Board(int sm, int jf, SpaceBounds jb, ImmutableArray<string> gn, Space[] bs, TitleDeed[] td, LuckCard[] chance, LuckCard[] chest)
        {
            StartingMoney = sm;
            JailFine = jf;
            JailBounds = jb;
            GroupNames = gn;
            BoardSpaces = bs;
            TitleDeeds = td;
            ChanceCards = new(chance);
            ChestCards = new(chest);
        }

        public async static Task<Board> BoardFromJson(Stream boardJson, Stream titleDeedStream, Stream chestStream, Stream chanceStream)
        {
            static SpaceBounds decodeBounds(JsonElement el) =>
                new(el.GetProperty("X").GetInt32(), el.GetProperty("Y").GetInt32(),
                    el.GetProperty("Width").GetInt32(), el.GetProperty("Height").GetInt32());
            
            var raw = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(boardJson) 
                ?? throw new JsonException("DeserializeAsync returned null");
            var startingMoney = raw["StartingMoney"].GetInt32();
            var jailFine = raw["JailFine"].GetInt32();
            var jailBounds = decodeBounds(raw["JailBounds"]);
            var groupNames = raw["GroupNames"].EnumerateArray().Select(n => n.GetString() ?? "null name").ToImmutableArray();
            var spaces = new List<Space>();
            foreach (var s in raw["BoardSpaces"].EnumerateArray())
            {
                var name = s.GetProperty("Name").GetString() ?? "null name";
                var bounds = decodeBounds(s.GetProperty("Bounds"));
                var type = s.GetProperty("Type").GetString();

                switch (type)
                {
                    case "Simple":
                        spaces.Add(new Space(name, bounds));
                        break;
                    case "Chest":
                        spaces.Add(new DrawCardSpace(name, CardType.Chest, bounds));
                        break;
                    case "Chance":
                        spaces.Add(new DrawCardSpace(name, CardType.Chance, bounds));
                        break;
                    case "GoToJail":
                        spaces.Add(new GoToJailSpace(name, bounds));
                        break;
                    case "Start":
                        var startValue = s.GetProperty("Value").GetInt32();
                        spaces.Add(new GoSpace(name, startValue, bounds));
                        break;
                    case "Tax":
                        var taxValue = s.GetProperty("Value").GetInt32();
                        spaces.Add(new TaxSpace(name, taxValue, bounds));
                        break;
                    case "TrainStation":
                        var trainValue = s.GetProperty("Value").GetInt32();
                        spaces.Add(new TrainStationSpace(name, trainValue, null, false, bounds));
                        break;
                    case "Utility":
                        var utilityValue = s.GetProperty("Value").GetInt32();
                        spaces.Add(new UtilitySpace(name, utilityValue, null, false, bounds));
                        break;
                    case "Road":
                        var roadValue = s.GetProperty("Value").GetInt32();
                        var roadGroup = s.GetProperty("Group").GetInt32();
                        spaces.Add(new RoadSpace(name, roadValue, roadGroup, null, false, default!, bounds));
                        break;
                    default:
                        throw new JsonException($"Invalid type \"{type}\"");
                }
            }

            var titleDeeds = await JsonSerializer.DeserializeAsync<TitleDeed[]>(titleDeedStream);
            var chanceCards = await JsonSerializer.DeserializeAsync<LuckCard[]>(chanceStream);
            var chestCards = await JsonSerializer.DeserializeAsync<LuckCard[]>(chestStream);

            return new Board(startingMoney, jailFine, jailBounds, groupNames, spaces.ToArray(), titleDeeds!, chanceCards!, chestCards!);
        }

        static int Position(string positionString) =>
            (char.ToLowerInvariant(positionString[0]) - 'a') * 10 + int.Parse(positionString[1..]);
            
        public int ParseBoardSpaceInt(string loc)
        {
            if (loc.Length != 2 || !char.IsLetter(loc[0]) || !char.IsDigit(loc[1]))
                throw new ArgumentException("Invalid argument format");

            var spacePos = Board.Position(loc.ToLowerInvariant());

            if (spacePos < 0 || spacePos > BoardSpaces.Length)
                throw new ArgumentException("Out of bounds");

            return spacePos;
        }

        public ref Space ParseBoardSpace(string loc) => ref BoardSpaces[ParseBoardSpaceInt(loc)];

        public TitleDeed TitleDeedFor(int loc)
        {
            if (BoardSpaces[loc] is not PropertySpace)
            {
                throw new ArgumentException("That is not a property space!");
            }

            return TitleDeeds.First(td => td.Position == loc);
        }

        public Color GroupColorOrDefault(Space space, Color? @default = null) =>
            space is RoadSpace rs ? Colors.ColorOfName(GroupNames[rs.Group]).ToDiscordColor() : @default ?? Color.Default;

        public IEnumerable<RoadSpace> FindSpacesOfGroup(int group) => BoardSpaces.Where(s => s is RoadSpace rs && rs.Group == group).Cast<RoadSpace>();

        public bool IsEntireGroupOwned(int group, out IEnumerable<RoadSpace> spaces)
        {
            var ss = FindSpacesOfGroup(group);
            spaces = ss;
            return ss.Count() == ss.Count(s => s.Owner is not null);
        }

        public int CountOwnedBy<T>(IUser player) where T : PropertySpace => BoardSpaces.Count(s => s is T ps && ps.Owner?.Id == player.Id);

        public int CalculateRentFor(int pos)
        {
            var deed = TitleDeedFor(pos);
            var space = (PropertySpace) BoardSpaces[pos];

            if (space.Owner is null || space.Mortgaged) // something has gone a little wrong
                return 0;

            if (space is RoadSpace rs)
            {
                return rs.Houses switch
                {
                    0 => deed.RentValues[0] * (IsEntireGroupOwned(rs.Group, out _) ? 2 : 1),
                    var x => deed.RentValues[x]
                };
            }
            if (space is TrainStationSpace)
            {
                var c = CountOwnedBy<TrainStationSpace>(space.Owner);
                return deed.RentValues[0] * c;
            }
            if (space is UtilitySpace)
            {
                var c = CountOwnedBy<UtilitySpace>(space.Owner);
                return c > 1 ? 10 : 4; // multiplication factor of dice roll
            }
            // something has gone wrong
            return 0;
        }
    }
}
