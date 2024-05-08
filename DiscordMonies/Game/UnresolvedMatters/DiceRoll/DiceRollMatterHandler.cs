using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;

namespace DiscordMonies.Game.UnresolvedMatters.DiceRoll;

public class DiceRollMatterHandler(GameState state, IFeedbackService service, BoardRenderer renderer) : IUnresolvedMatterResolver<DiceRollMatter>
{
    public async Task Resolve(DiceRollMatter matter)
    {
        if (state.Dice.DoubleRoll)
        {
            await service.SendAsync(state.ThreadId, $"{Mention.User(state.CurrentPlayer)}",
                embeds: new([Embeds.TurnAdvancedDoubles(state.CurrentPlayer)]),
                options: new()
                {
                    Attachments = new([renderer.RenderToRemoraAttachment([new(state.CurrentPlayer.CurrentSpace, state.CurrentPlayer.Color.Color)])]),
                    MessageComponents = new([
                        new ActionRowComponent([
                            Buttons.Roll()
                        ])
                    ])
                });
            return;
        }
        
        state.Dice.Reset();

        List<IMessageComponent> buttons = [
            Buttons.Roll()
        ];
        
        if (state.CurrentPlayer.JailTurns != -1)
        {
            buttons.Insert(0, Buttons.Bail(state.Board.JailFine));
            
            if (state.Board.GambleCards.JailCardOwner == state.CurrentPlayer)
            {
                buttons.Insert(0, Buttons.UseJailCard("gamble"));
            }
            if (state.Board.TreasureCards.JailCardOwner == state.CurrentPlayer)
            {
                buttons.Insert(0, Buttons.UseJailCard("treasure"));
            }
        }
        
        await service.SendAsync(state.ThreadId, $"{Mention.User(state.CurrentPlayer)}",
            embeds: new([Embeds.TurnAdvanced(state.CurrentPlayer)]),
            options: new()
            {
                Attachments = new([renderer.RenderToRemoraAttachment([new(state.CurrentPlayer.CurrentSpace, state.CurrentPlayer.Color.Color)])]),
                MessageComponents = new([
                    new ActionRowComponent(buttons)
                ])
            });
    }
}