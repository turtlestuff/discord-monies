using DiscordMonies.Game.UnresolvedMatters;

namespace DiscordMonies.Game.Trade;

public interface IOwnable
{
    public GamePlayer? Owner { get; }

    public string ToString();
    
    IUnresolvedMatter? GiveTo(GamePlayer? target, List<GamePlayer> players);
};