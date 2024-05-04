using DiscordMonies.Board;

namespace DiscordMonies.Game.UnresolvedMatters.MortgageTransfer;

public class MortgageTransferMatter : IUnresolvedMatter
{
    public GameAction Action => GameAction.MortgageTransferDecision;
    
    public required GamePlayer Player { get; set; }
    
    public required Space Space { get; set; }
}