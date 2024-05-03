using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;

namespace DiscordMonies.Game.UnresolvedMatters.PayOrGamble;

public class PayOrGambleMatterHandler(GameState state, IFeedbackService service) : IUnresolvedMatterResolver<PayOrGambleMatter>
{
    public async Task Resolve(PayOrGambleMatter matter)
    {
        await service.SendAsync(state.ThreadId, $"{Mention.User(matter.Player)}, what do you want to do?", options: new()
        {
            MessageComponents = new([
                new ActionRowComponent([
                    Buttons.TakeCard(),
                    Buttons.TakeCardPay(matter.PayValue)
                ])
            ])
        });
    }
}