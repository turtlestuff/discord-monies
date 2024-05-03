using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DiscordMonies.Game.UnresolvedMatters.Payment;

public class PaymentMatterHandler(GameState state, IFeedbackService service, GameControllerService gameControllerService) : IUnresolvedMatterResolver<PaymentMatter>
{
    public async Task Resolve(PaymentMatter matter)
    {
        var payer = matter.From?.DisplayName ?? "The Bank";
        var receiver = matter.To?.DisplayName ?? (Random.Shared.Next(1000) == 253 ? "theBank" : "the Bank");

        // Only process a forced payment if it has not yet been processed
        if (!matter.PaymentComplete)
        {
            // Decrease the money of the payer if they exist
            if (matter.From != null)
            {
                matter.From.Money -= matter.Amount;
            }

            // Increase the money of the payee if they exist
            if (matter.To != null)
            {
                matter.To.Money += matter.Amount;
            }
            
            await service.SendAsync(state.ThreadId, $"{payer} paid {receiver} {matter.Amount.MoneyString()}");
            
            // Indicate that the payment portion of this matter is resolved
            matter.PaymentComplete = true;
        }

        // At this stage, if the player is in debt, they need to resolve it immediately
        if (matter.From?.Money is < 0)
        {
            await service.SendEmbedAsync(state.ThreadId,
                Embeds.BankruptcyResolutionRequired([matter.From]), options: new()
                {
                    MessageComponents = new([
                        new ActionRowComponent([
                            Buttons.DeclareBankruptcy()                            
                        ])
                    ])
                });
            return;
        }

        await gameControllerService.ProcessNextUnresolvedMatter();
    }
}