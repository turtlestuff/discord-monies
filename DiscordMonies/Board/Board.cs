using System.Collections.Immutable;

using DiscordMonies.Game;

namespace DiscordMonies.Board;

public sealed record Board(
    int StartingMoney,
    int JailFine,
    SpaceBounds JailBounds,
    ImmutableArray<string> GroupNames,
    ImmutableArray<BoardSpace> BoardSpaces);

public sealed record TitleDeed(
    int Position,
    ImmutableArray<int> RentValues,
    int MortgageValue,
    int HouseCost,
    int HotelCost);

public sealed record LuckCard(
    string Description,
    string Command);

public sealed record GameBoard
{
    public Board BoardInfo { get; }
    public ImmutableArray<TitleDeed> TitleDeeds { get; }
    public ImmutableArray<LuckCard> AllChanceCards { get; }
    public Stack<LuckCard> UnusedChanceCards { get; }
    public ImmutableArray<LuckCard> AllChestCards { get; }
    public Stack<LuckCard> UnusedChestCards { get; }

    public Dictionary<PropertySpace, SpaceState> SpaceStates { get; }
    public Dictionary<RoadSpace, int> HouseCounts { get; }

    public int VisitingJailPosition { get; }
    public int PassGoValue { get; }

    public GameBoard(
        BoardInfo info)
    {
        BoardInfo = info.Board;
        TitleDeeds = info.TitleDeeds;
        AllChanceCards = info.ChanceCards;
        AllChestCards = info.ChestCards;

        UnusedChanceCards = new(info.ChanceCards);
        UnusedChestCards = new(info.ChestCards);

        VisitingJailPosition = BoardInfo.BoardSpaces.Select((s, i) => (Space: s, Index: i)).First(e => e.Space.Name == "Visiting Jail").Index;
        PassGoValue = BoardInfo.BoardSpaces.OfType<GoSpace>().First().Value;

        SpaceStates = info.Board.BoardSpaces.OfType<PropertySpace>().ToDictionary(p => p, _ => new SpaceState());
        HouseCounts = info.Board.BoardSpaces.OfType<RoadSpace>().ToDictionary(p => p, _ => 0);
    }
}

public sealed class SpaceState
{
    public GamePlayer? Owner { get; set; }
    public bool IsMortgaged { get; set; }
}
