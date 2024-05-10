using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;

namespace DiscordMonies.Game.UnresolvedMatters.MortgageTransfer;

public class MortgageTransferMatterHandler(GameState state, IFeedbackService service) : IUnresolvedMatterResolver<MortgageTransferMatter>
{
    public async Task Resolve(MortgageTransferMatter matter)
    {
        // TODO: Ensure the property is still mortgage and ignore if it is not
        await service.SendAsync(state.ThreadId, $"{Mention.User(matter.Player)}, you have received a mortgaged property.",
            embeds: new([Embeds.MortgageTransfer(matter.Space)]),
            options: new()
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