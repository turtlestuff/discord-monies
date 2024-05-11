using System.Collections.Immutable;
using System.Drawing;

using DiscordMonies.Game;
using DiscordMonies.Game.Trade;
using DiscordMonies.Game.UnresolvedMatters;
using DiscordMonies.Game.UnresolvedMatters.AuctionMatter;
using DiscordMonies.Game.UnresolvedMatters.MortgageTransfer;

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
    int HotelCost)
{
    public int UnmortgageValue => (int)(MortgageValue * 1.1);
};

public sealed record LuckCard(
    string Description,
    string Command);

public record ColorSet(int Index);

public class Space : IOwnable
{
    public required GameBoard Board { get; init; }
    
    public required BoardSpace Info { get; init; }

    public required TitleDeed? TitleDeed { get; init;  }

    public GamePlayer? Owner { get; set; }

    public int BuiltHouses { get; set; }
    
    public bool IsMortgaged => BuiltHouses == -1;
    
    public int Index { get; set; }

    public string PositionCode => $"{(char)('A' + Index / 10)}{Index % 10}";

    public string PositionString => Index % 10 == 0 ? string.Empty : $" ({PositionCode})";

    public string NameWithPositionString =>
        ToString() + (string.IsNullOrEmpty(PositionString) ? string.Empty : PositionString);

    public override string ToString() => NameWithPositionString;

    public Color GroupColorOrDefault(Color defaultColor) 
        => Info is RoadSpace rs ? PrettyColors.ColorMap[Board.GroupNames[rs.Group]] : defaultColor;

    public IUnresolvedMatter? GiveTo(GamePlayer? target, List<GamePlayer> players)
    {
        Owner = target;
        
        if (target is null)
        {
            // If the target is the bank, it needs to be auctioned off and unmortgaged
            if (BuiltHouses < 0)
            {
                BuiltHouses = 0;
            }
            
            return new AuctionMatter
            {
                Player = players.First(),
                BiddingPlayers = players,
                AuctionSpace = this
            };
        }
        else if (IsMortgaged)
        {
            // If the property is mortgaged, ask the target if they want to pay the mortgage transfer fee
            return new MortgageTransferMatter
            {
                Player = target,
                Space = this
            };
        }
        
        return null;
    }
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

    public int HotelsRemaining => 12 - Spaces.Select(x => x.BuiltHouses).Count(houses => houses == 5);
    
    public int HousesRemaining => 32 - Spaces.Select(x => x.BuiltHouses).Where(houses => houses != 5 && houses != -1).Sum();

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
