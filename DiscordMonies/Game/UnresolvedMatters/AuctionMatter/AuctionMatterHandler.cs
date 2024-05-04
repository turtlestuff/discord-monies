using DiscordMonies.Remora;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;

namespace DiscordMonies.Game.UnresolvedMatters.AuctionMatter;

public class AuctionMatterHandler(GameState state, IFeedbackService service, GameControllerService gameController) : IUnresolvedMatterResolver<AuctionMatter>
{
    public async Task Resolve(AuctionMatter matter)
    {
        if (matter.BiddingPlayers.Count == 1 && matter.HighestBid > 0)
        {
            // The property goes to the last player
            await service.SendEmbedAsync(state.ThreadId, Embeds.AuctionComplete(matter.AuctionSpace, matter.BiddingPlayers[0], matter.HighestBid));
            matter.AuctionSpace.Owner = matter.BiddingPlayers[0];
            await gameController.Pay(matter.BiddingPlayers[0], null, matter.HighestBid);
            await gameController.ProcessNextUnresolvedMatter();
            return;
        }

        if (matter.BiddingPlayers.Count == 0)
        {
            // The property goes back to the bank
            await service.SendEmbedAsync(state.ThreadId, Embeds.AuctionFailure(matter.AuctionSpace));
            matter.AuctionSpace.Owner = null;
            await gameController.ProcessNextUnresolvedMatter();
            return;
        }
        
        var embed = Embeds.Auction(matter.AuctionSpace, matter.HighestBid, matter.HighestBidder);
        List<ActionRowComponent> messageComponents = [
            new([
                Buttons.Bid(1, matter.HighestBid),
                Buttons.Bid(5, matter.HighestBid),
                Buttons.Bid(10, matter.HighestBid),
                Buttons.Bid(20, matter.HighestBid),
                Buttons.Bid(100, matter.HighestBid)
            ]),
            new([
                Buttons.AuctionBidOther(),
                Buttons.AuctionTitleDeed(),
                Buttons.DropAuction()
            ])
        ];
        
        if (!matter.Message.HasValue)
        {
            matter.BiddingPlayers = state.Players.ToList();
        }
            
        var response = await service.SendAsync(state.ThreadId, $"{Mention.User(matter.Player!)}" , embeds: new([embed]), options: new()
        {
            MessageComponents = messageComponents
        });
            
        matter.Message = response.Get().ID;
    }
}