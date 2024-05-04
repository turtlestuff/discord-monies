using DiscordMonies.Board;
using Remora.Rest.Core;

namespace DiscordMonies.Game.UnresolvedMatters.AuctionMatter;

public class AuctionMatter : IUnresolvedMatter
{
    public GameAction Action => GameAction.AuctionBid;
    
    public required GamePlayer? Player { get; set; }
    
    public required Space AuctionSpace { get; set; }
    
    public Snowflake? Message { get; set; }

    public required List<GamePlayer> BiddingPlayers { get; set; } = [];
    
    public int HighestBid { get; set; }

    public GamePlayer? HighestBidder { get; set; }
}
