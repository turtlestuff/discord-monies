using DiscordMonies.Board;

namespace DiscordMonies.Game.UnresolvedMatters.PayOrGamble;

public class PayOrGambleMatter : IUnresolvedMatter
{
    public GameAction Action => GameAction.TakeCardOrPayDecision;
    public required GamePlayer Player { get; init; }
    
    public required int PayValue { get; set; }
    
    public required CardStack CardStack { get; set; }
}