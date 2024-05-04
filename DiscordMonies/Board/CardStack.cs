using System.Collections.Immutable;
using DiscordMonies.Game;
using DiscordMonies.Game.Trade;
using DiscordMonies.Game.UnresolvedMatters;

namespace DiscordMonies.Board;

// IOwnable because of the jail card inside
public class CardStack(ImmutableArray<LuckCard> allCards) : IOwnable
{
    public ImmutableArray<LuckCard> AllCards => allCards;

    public Stack<LuckCard> UnusedCards { get; private set; } = new();
    
    public GamePlayer? JailCardOwner { get; set; }
    
    public LuckCard DrawCard()
    {
        if (UnusedCards.Count == 0)
        {
            // TODO: Get out of jail free cards
            UnusedCards = new(AllCards.OrderBy(_ => Guid.NewGuid()).ToList());
        }

        return UnusedCards.Pop();
    }

    GamePlayer? IOwnable.Owner => JailCardOwner;

    string IOwnable.ToString() => "Get Out of Jail Free card";
    
    public IUnresolvedMatter? GiveTo(GamePlayer? target)
    {
        JailCardOwner = target;
        return null;
    }
}
