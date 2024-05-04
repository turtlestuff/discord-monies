using DiscordMonies.Board;
using DiscordMonies.Game.Trade;
using DiscordMonies.Game.UnresolvedMatters;
using DiscordMonies.Game.UnresolvedMatters.DiceRoll;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

namespace DiscordMonies.Game;

public partial class GameState
{
    public required int GameId { get; init; }

    public required Snowflake ThreadId { get; init; }

    public required Snowflake GuildId { get; init; }

    public required List<Snowflake> PlayerIds { get; init; }

    public List<GamePlayer> Players { get; } = [];

    public int CurrentPlayerIndex { get; set; }
    
    public GamePlayer CurrentPlayer => Players[CurrentPlayerIndex];

    public GamePlayer? TryGetById(Snowflake userId) 
        => Players.FirstOrDefault(p => p.Id == userId);

    public GamePlayer? TryGetById(IUser user) => TryGetById(user.ID);

    public GamePlayer GetById(Snowflake userId)
        => Players.First(p => p.Id == userId);

    public GamePlayer GetById(IUser user) => GetById(user.ID);
    
    public required GameBoard Board { get; init; }

    public GameDice Dice { get; } = new();

    public IUnresolvedMatter CurrentlyResolvingMatter { get; set; } = null!;

    public List<IUnresolvedMatter> UnresolvedMatters { get; } = [];

    public IList<TradeTable> TradeTables { get; } = [];
}

public enum GameAction
{
    Roll,
    PurchaseDecision,
    AuctionBid,
    RequiredBailPayment,
    BankruptcyResolution,
    TakeCardOrPayDecision,
    MortgageTransferDecision
}
