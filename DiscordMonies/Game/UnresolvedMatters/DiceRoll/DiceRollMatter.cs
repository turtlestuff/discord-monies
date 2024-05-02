namespace DiscordMonies.Game.UnresolvedMatters.DiceRoll;

public class DiceRollMatter : IUnresolvedMatter
{
    public GameAction Action => GameAction.Roll;

    public required GamePlayer Player { get; set; }
}