using Remora.Discord.Commands.Feedback.Services;

namespace DiscordMonies.Game.UnresolvedMatters.RequiredBailPaymentComplete;

public class RequiredBailPaymentCompleteMatterHandler(GameState state, IFeedbackService service, GameControllerService controllerService) : IUnresolvedMatterResolver<RequiredBailPaymentCompleteMatter>
{
    public async Task Resolve(RequiredBailPaymentCompleteMatter matter)
    {
        await controllerService.MovePlayerRelative(matter.Player, matter.Spaces);
        await service.SendEmbedAsync(state.ThreadId, Embeds.JailBail(matter.Player, state.Board.JailFine, matter.Player.CurrentSpace));
        await controllerService.ProcessNextUnresolvedMatter();
    }
}