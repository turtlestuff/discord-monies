using DiscordMonies.Board;
using DiscordMonies.Extensions;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;

namespace DiscordMonies.Game.UnresolvedMatters.PurchaseDecision;

public class PurchaseDecisionMatterHandler(GameState state, IFeedbackService service) : IUnresolvedMatterResolver<PurchaseDecisionMatter>
{
    public async Task Resolve(PurchaseDecisionMatter matter)
    {
        var property = (PropertySpace) matter.Space.Info;
        
        // Purchase this property?
        await service.SendAsync(state.ThreadId,
            $"{Mention.User(state.CurrentPlayer)} - **{matter.Space}** is available for purchase! Do you want to purchase it?",
            embeds: new([matter.Space.TitleDeedEmbed()]),
            options: new()
            {
                MessageComponents = new([
                    new ActionRowComponent([
                        Buttons.Purchase(matter.Space.ToString(), property.Value),
                        Buttons.SendToAuction()
                    ])
                ])
            });
    }
}