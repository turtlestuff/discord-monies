using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;

namespace DiscordMonies.Game.UnresolvedMatters.RequiredBailPayment;

public class RequiredBailPaymentMatterHandler(GameState state, IFeedbackService service) : IUnresolvedMatterResolver<RequiredBailPaymentMatter>
{
    public async Task Resolve(RequiredBailPaymentMatter matter)
    {
        List<IMessageComponent> buttons = [
            Buttons.Bail(state.Board.JailFine)  
        ];
        
        if (state.Board.GambleCards.JailCardOwner == state.CurrentPlayer)
        {
            buttons.Insert(0, Buttons.UseJailCard("gamble"));
        }
        if (state.Board.TreasureCards.JailCardOwner == state.CurrentPlayer)
        {
            buttons.Insert(0, Buttons.UseJailCard("treasure"));
        }
        
        await service.SendContextualEmbedAsync(Embeds.JailDieRolled(state.CurrentPlayer, state.Dice, 0), new()
        {
            MessageComponents = new([
                new ActionRowComponent(buttons)
            ])
        });
    }
}