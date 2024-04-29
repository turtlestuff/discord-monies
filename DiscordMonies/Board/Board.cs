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

public sealed class Space
{
    public required GameBoard Board { get; init; }
    
    public required BoardSpace Info { get; init; }

    public required TitleDeed? TitleDeed { get; init;  }

    public GamePlayer? Owner { get; set; }

    public int BuiltHouses { get; set; }
    
    public bool IsMortgaged => BuiltHouses == -1;
    
    public int Index { get; set; }

    public string PositionString => Index % 10 == 0 ? string.Empty : $" ({(char) ('A' + Index / 10)}{Index % 10})";

    public string NameWithPositionString =>
        ToString() + (string.IsNullOrEmpty(PositionString) ? string.Empty : PositionString);

    public override string ToString() => $"{Info.Name}";
}

public sealed record GameBoard
{
    private Board Info { get; }

    public ImmutableArray<TitleDeed> TitleDeeds { get; }
    
    public CardStack TreasureCards { get; }
    
    public CardStack GambleCards { get; }

    public ImmutableArray<Space> Spaces { get; }

    public int PassGoValue { get; }

    public int JailFine => Info.JailFine;

    public SpaceBounds JailBounds => Info.JailBounds;

    public ImmutableArray<string> GroupNames => Info.GroupNames;

    public GameBoard(
        BoardInfo info)
    {
        Info = info.Board;
        TitleDeeds = info.TitleDeeds;

        GambleCards = new(info.ChanceCards);
        TreasureCards = new(info.ChestCards);

        PassGoValue = Info.BoardSpaces.OfType<GoSpace>().First().Value;

        Spaces = [ .. 
            from space in info.Board.BoardSpaces.Select((s, i) => (Space: s, Index: i))
            join deed in info.TitleDeeds on space.Index equals deed.Position into spaceDeed
            from deed in spaceDeed.DefaultIfEmpty()
            select new Space { Board = this, Info = space.Space, Index = space.Index, TitleDeed = deed }];
    }
}
