using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DiscordMonies.Game.UnresolvedMatters.RequiredBailPayment;

public class RequiredBailPaymentMatterHandler(GameState state, IFeedbackService service) : IUnresolvedMatterResolver<RequiredBailPaymentMatter>
{
    public async Task Resolve(RequiredBailPaymentMatter matter)
    {
        await service.SendContextualEmbedAsync(Embeds.FinalJailDieRolled(state.CurrentPlayer, state.Dice), new()
        {
            MessageComponents = new([
                new ActionRowComponent([
                    Buttons.Bail(state.Board.JailFine)                        
                ])
            ])
        });
    }
}