namespace DiscordMonies.Game.UnresolvedMatters.RequiredBailPaymentComplete;

public class RequiredBailPaymentCompleteMatter : IUnresolvedMatter
{
    public GameAction Action => GameAction.Roll;
    
    public required GamePlayer Player { get; init; }
    
    public required int Spaces { get; init; }
    
    public required bool JailCard { get; set; }
}