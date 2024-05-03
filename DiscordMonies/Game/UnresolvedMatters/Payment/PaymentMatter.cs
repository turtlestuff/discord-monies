namespace DiscordMonies.Game.UnresolvedMatters.Payment;

public class PaymentMatter : IUnresolvedMatter
{
    public required GamePlayer? From { get; set; }
    
    public required GamePlayer? To { get; set; }
    
    public required int Amount { get; set; }

    public GameAction Action => GameAction.BankruptcyResolution;

    public GamePlayer? Player => From;
    
    public bool PaymentComplete { get; set; }
}
