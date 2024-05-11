using DiscordMonies.Board;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;

namespace DiscordMonies.Game.UnresolvedMatters.DiceRoll;

public class DiceRollMatterHandler(GameState state, IDiscordRestChannelAPI restChannelApi, BoardRenderer renderer) : IUnresolvedMatterResolver<DiceRollMatter>
{
    public async Task Resolve(DiceRollMatter matter)
    {
        if (state.Dice.DoubleRoll)
        {
            var sendResult = await restChannelApi.CreateMessageAsync(state.ThreadId, $"{Mention.User(state.CurrentPlayer)}, it's your turn.",
                embeds: new([Embeds.TurnAdvancedDoubles(state.CurrentPlayer)]),
                components: new([
                    new ActionRowComponent([
                        Buttons.Roll()
                    ])
                ]),
                attachments: new([
                    renderer.RenderToRemoraAttachment([
                        new(state.CurrentPlayer.CurrentSpace, state.CurrentPlayer.Color.Color)
                    ])
                ]),
                messageReference: state.LastRollMessage is null ? default(Optional<IMessageReference>) : new MessageReference(state.LastRollMessage.Value)
            );
            state.LastRollMessage = sendResult.Entity.ID;
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
        
        var result = await restChannelApi.CreateMessageAsync(state.ThreadId, $"{Mention.User(state.CurrentPlayer)}, it's your turn.",
            embeds: new([Embeds.TurnAdvanced(state.CurrentPlayer)]),
            components: new([
                new ActionRowComponent(buttons)
            ]),
            attachments: new([
                renderer.RenderToRemoraAttachment([
                    new(state.CurrentPlayer.CurrentSpace, state.CurrentPlayer.Color.Color)
                ])
            ]),
            messageReference: state.LastRollMessage is null ? default(Optional<IMessageReference>) : new MessageReference(state.LastRollMessage.Value)
        );
        state.LastRollMessage = result.Entity.ID;
    }
}