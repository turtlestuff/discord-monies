using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DiscordMonies.Game.UnresolvedMatters.Payment;

public class PaymentMatterHandler(GameState state, IFeedbackService service, GameControllerService gameControllerService) : IUnresolvedMatterResolver<PaymentMatter>
{
    public async Task Resolve(PaymentMatter matter)
    {
        var payer = matter.From?.DisplayName ?? "The Bank";
        var receiver = matter.To?.DisplayName ?? (Random.Shared.Next(1000) == 253 ? "theBank" : "the Bank");

        // Decrease the money of the payer if they exist
        if (matter.From != null)
        {
            if (matter.From.Money < matter.Amount)
            {
                // Not enough money to resolve this matter
                await service.SendEmbedAsync(state.ThreadId,
                    Embeds.BankruptcyResolutionRequired(state.Players.Where(x => x.Money < 0)), options: new()
                    {
                        MessageComponents = new([
                            new ActionRowComponent([
                                Buttons.DeclareBankruptcy()                            
                            ])
                        ])
                    });
            }
            
            matter.From.Money -= matter.Amount;
        }

        // Increase the money of the payee if they exist
        if (matter.To != null)
        {
            matter.To.Money += matter.Amount;
        }

        await service.SendAsync(state.ThreadId, $"{payer} paid {receiver} {matter.Amount.MoneyString()}");
        await gameControllerService.ProcessNextUnresolvedMatter();
    }
}