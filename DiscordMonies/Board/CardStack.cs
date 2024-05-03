using System.Collections.Immutable;
using DiscordMonies.Game;

namespace DiscordMonies.Board;

public class CardStack(ImmutableArray<LuckCard> allCards)
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
}
