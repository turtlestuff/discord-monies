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
        public int StartingMoney { get; }
        public SpaceBounds JailBounds { get; }
        public ImmutableArray<string> GroupNames { get; }
        public Space[] BoardSpaces { get; }

        Board(int sm, SpaceBounds jb, ImmutableArray<string> gn, Space[] bs)
        {
            StartingMoney = sm;
            JailBounds = jb;
            GroupNames = gn;
            BoardSpaces = bs;
        }

        public async static Task<Board> BoardFromJson(Stream jsonStream)
        {
            static SpaceBounds decodeBounds(JsonElement el) =>
                new(el.GetProperty("X").GetInt32(), el.GetProperty("Y").GetInt32(),
                    el.GetProperty("Width").GetInt32(), el.GetProperty("Height").GetInt32());

            var raw = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(jsonStream) 
                ?? throw new JsonException("DeserializeAsync returned null");
            var startingMoney = raw["StartingMoney"].GetInt32();
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

            return new Board(startingMoney, jailBounds, groupNames, spaces.ToArray());
        }

        public static int BoardPosition(string positionString) =>
            (char.ToLowerInvariant(positionString[0]) - 'a') * 10 + int.Parse(positionString[1..]);
    }
}
