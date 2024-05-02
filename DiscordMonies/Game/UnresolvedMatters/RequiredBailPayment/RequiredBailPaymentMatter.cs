namespace DiscordMonies.Game.UnresolvedMatters.RequiredBailPayment;

public class RequiredBailPaymentMatter : IUnresolvedMatter
{
    public GameAction Action => GameAction.RequiredBailPayment;
    
    public required GamePlayer? Player { get; init; }
}