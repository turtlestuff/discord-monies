using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DiscordMonies.Game.UnresolvedMatters.MortgageTransfer;

public class MortgageTransferMatterHandler(GameState state, IFeedbackService service) : IUnresolvedMatterResolver<MortgageTransferMatter>
{
    public async Task Resolve(MortgageTransferMatter matter)
    {
        // TODO: Ensure the property is still mortgage and ignore if it is not
        await service.SendEmbedAsync(state.ThreadId,
            Embeds.MortgageTransfer(matter.Space), options: new()
            {
                MessageComponents = new([
                    new ActionRowComponent([
                        Buttons.MortgageTransferUnmortgage(matter.Space),
                        Buttons.MortgageTransferKeepMortgage(matter.Space)
                    ])
                ])
            });
    }
}