namespace DiscordMonies.Game.UnresolvedMatters;

public interface IUnresolvedMatter
{
    public GameAction Action { get; }
    
    /// <summary>
    /// Specifies the player that this matter concerns. If the player is bankrupt before this matter can be resolved,
    /// this matter will not be processed and will be removed from the queue.
    /// </summary>
    public GamePlayer? Player { get; }
}
