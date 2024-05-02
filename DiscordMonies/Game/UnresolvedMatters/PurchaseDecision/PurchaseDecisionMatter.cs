using DiscordMonies.Board;

namespace DiscordMonies.Game.UnresolvedMatters.PurchaseDecision;

public class PurchaseDecisionMatter : IUnresolvedMatter
{
    public GameAction Action => GameAction.PurchaseDecision;

    GamePlayer IUnresolvedMatter.Player => Purchaser;

    public required Space Space { get; set; }
    
    public required GamePlayer Purchaser { get; set; }
}