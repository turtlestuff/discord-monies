using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using DiscordMonies.Board;
using DiscordMonies.Extensions;
using DiscordMonies.Game.Trade;
using DiscordMonies.Game.UnresolvedMatters;
using DiscordMonies.Game.UnresolvedMatters.AuctionMatter;
using DiscordMonies.Game.UnresolvedMatters.DiceRoll;
using DiscordMonies.Game.UnresolvedMatters.Payment;
using DiscordMonies.Remora;
using OneOf.Types;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace DiscordMonies.Game;

public class GameCommands(GameState gameState, IFeedbackService feedback, IInteractionCommandContext ctx, GameControllerService gameControllerService, IDiscordRestInteractionAPI interactionApi, BoardRenderer renderer) : CommandGroup
{
    [Command("drop")]
    [Description("Leave the game")]
    [RequiresPlayer(PlayerType.Player)]
    [Ephemeral]
    [SuppressInteractionResponse(true)]
    public async Task<Result> Drop()
    {
        var player = gameState.GetById(ctx.GetUserId());
        if (gameState.CurrentlyResolvingMatter.Action == GameAction.AuctionBid)
        {
            await feedback.SendContextualAsync("Please wait for the auction to complete before you drop.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return Result.Success;
        }
        
        if (gameState.CurrentlyResolvingMatter is not DiceRollMatter and not PaymentMatter && gameState.CurrentPlayer == player)
        {
            await feedback.SendContextualAsync("Please complete your turn before you drop.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return Result.Success;
        }
        
        return await interactionApi.CreateInteractionResponseAsync(ctx.Interaction.ID, ctx.Interaction.Token,
            new InteractionResponse(InteractionCallbackType.Modal, new(new InteractionModalCallbackData(
                CustomIDHelpers.CreateModalID("do-drop"),
                "Drop from Discord Monies Game",
                [
                    new ActionRowComponent([
                        new TextInputComponent(
                            "drop-response",
                            TextInputStyle.Short,
                            "Do you want to drop from this game? (Y/N)",
                            1,
                            1,
                            true,
                            default,
                            "Y"
                        )])
                ]
            ))));
    }
    
    [Command("balance")]
    [Description("Obtain the balance for a player")]
    [RequiresPlayer(PlayerType.PlayerOrSpectator)]
    [Ephemeral]
    public async Task<Result> Balance([Description("The player to obtain the balance for")] GamePlayer? player = null)
    {
        player ??= gameState.GetById(ctx.GetUserId());
        await feedback.SendContextualAsync($"{player}'s balance: {player.Money.MoneyString()}");
        return Result.Success;
    }

    [Command("roll")]
    [Description("Roll the die")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.Roll, ErrorMessage = "You can't roll now.")]
    public async Task<Result> Roll()
    {
        await gameControllerService.RollDie();
        return Result.Success;
    }

    [Command("nudge")]
    [Description("Ping the current player and ask them to complete their turn")]
    [RequiresPlayer(PlayerType.Player)]
    [SuppressInteractionResponse(true)]
    public async Task<Result> Nudge()
    {
        // TODO: Auctions
        var current = gameState.CurrentPlayer;
        await feedback.SendContextualAsync(
            $"{Mention.User(current.Id)}, {gameState.GetById(ctx.GetUserId()).DisplayName} wished to remind you that it is your turn to play by giving you a gentle nudge. *Nudge!*");
        return Result.Success;
    }

    [Command("deed")]
    [Description("View the title deed for a property")]
    [RequiresPlayer(PlayerType.PlayerOrSpectator)]
    [Ephemeral]
    public async Task<Result> Deed([Description("The space to view the title deed for")] [Autocomplete] Space space)
    {
        // TODO: Ensure that the space has a title deed
        await feedback.SendContextualAsync($"Title deed for {space}", embeds: new([space.TitleDeedEmbed()]));
        return Result.Success;
    }

    [Command("board")]
    [Description("View the current state of the board")]
    [RequiresPlayer(PlayerType.PlayerOrSpectator)]
    [Ephemeral]
    public async Task<Result> Board()
    {
        await feedback.SendContextualAsync("", options: new()
        {
            Attachments = new([
                new FileData("board.png", renderer.RenderToStream())
            ])
        });
        return Result.Success;
    }
    
    [Command("bail")]
    [Description("Pay the jail fine and leave jail")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.Roll, GameAction.RequiredBailPayment, ErrorMessage = "You can't bail from jail right now")]
    [Ephemeral]
    public async Task<Result> Bail()
    {
        await gameControllerService.Bail();
        return Result.Success;
    }

    [Command("develop")]
    [Description("Build or demolish houses or hotels on properties")]
    [RequiresPlayer(PlayerType.Player)]
    [SuppressInteractionResponse(true)]
    [CommandType(ApplicationCommandType.ChatInput)]
    [Ephemeral]
    public async Task<Result> Develop([Description("The colour set to build on")] [Autocomplete] ColorSet colorSet)
    {
        var player = gameState.GetById(ctx.GetUserId());
        var options = gameControllerService.DevelopOptions(player, colorSet, out var notDevelopableReason);

        var boardGroupName = gameState.Board.GroupNames[colorSet.Index];
        if (options.Count == 0)
        {
            await feedback.SendContextualAsync(notDevelopableReason switch
            {
                GameControllerService.NotDevelopableReason.Developable => throw new UnreachableException(),
                GameControllerService.NotDevelopableReason.NotOwned => $"You can't develop on the {boardGroupName} color set because you don't own the entire set.",
                GameControllerService.NotDevelopableReason.Mortgaged => $"You can't develop on the {boardGroupName} color set because there are mortgaged properties on the set.",
                _ => throw new ArgumentOutOfRangeException()
            }, options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return Result.Success;
        }
        
        var response = await interactionApi.CreateInteractionResponseAsync(ctx.Interaction.ID, ctx.Interaction.Token,
            new InteractionResponse(InteractionCallbackType.Modal, new(new InteractionModalCallbackData(
                CustomIDHelpers.CreateModalIDWithState("do-develop", $"{colorSet.Index}"),
                $"Develop properties - {boardGroupName}",
                options.Select((space, i) => space.IsMortgaged ? null : new ActionRowComponent([new TextInputComponent(
                    $"space-{i}",
                    TextInputStyle.Short,
                    $"Houses on {(space.ToString().Length > 15 ? $"{space.ToString()[..15]}..." : space)} (5 for hotel)",
                    1,
                    1,
                    true,
                    $"{space.BuiltHouses}",
                    "Number of houses"
                )])).Where(component => component is not null).ToImmutableList()!
            ))));
        return response;
    }

    [Command("mortgage")]
    [Description("Mortgage/demortgage a property")]
    [RequiresPlayer(PlayerType.Player)]
    [Ephemeral]
    public async Task<Result> Mortgage([Description("The space to mortgage/demortgage")] [AutocompleteProvider("autocomplete::spaces:owned")] Space space)
    {
        var player = gameState.GetById(ctx.GetUserId());
        await gameControllerService.Mortgage(player, space);
        return Result.Success;
    }
}

[Group("property")]
public class GamePropertyCommands(
    GameControllerService gameControllerService) : CommandGroup
{
    [Command("purchase")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.PurchaseDecision, ErrorMessage = "There's nothing to purchase right now")]
    [Ephemeral]
    public async Task<Result> Purchase()
    {
        await gameControllerService.BuyProperty();
        return Result.Success;
    }
    
    [Command("sendToAuction")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.PurchaseDecision, ErrorMessage = "There's nothing to send to auction right now")]
    public async Task<Result> SendToAuction()
    {
        await gameControllerService.SendPropertyToAuction();
        return Result.Success;
    }
}

public class GameInteractions(GameState gameState, IFeedbackService feedback, IInteractionCommandContext ctx, GameControllerService gameControllerService, IDiscordRestInteractionAPI interactionApi) : InteractionGroup
{
    [Button("roll")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.Roll, ErrorMessage = "You can't roll now.")]
    public async Task<Result> Roll()
    {
        await gameControllerService.RollDie();
        return Result.Success;
    }

    [Button("purchase")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.PurchaseDecision, ErrorMessage = "There's nothing to purchase right now")]
    [Ephemeral]
    public async Task<Result> Purchase()
    {
        await gameControllerService.BuyProperty();
        return Result.Success;
    }
    
    [Button("auction")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.PurchaseDecision, ErrorMessage = "There's nothing to send to auction right now")]
    public async Task<Result> SendToAuction()
    {
        await gameControllerService.SendPropertyToAuction();
        return Result.Success;
    }
    
    [Button("bail")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.Roll, GameAction.RequiredBailPayment, ErrorMessage = "You can't bail from jail right now")]
    [Ephemeral]
    public async Task<Result> Bail()
    {
        await gameControllerService.Bail();
        return Result.Success;
    }
    
    [Button("use-jail-card")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.Roll, GameAction.RequiredBailPayment, ErrorMessage = "You can't use your Get Out of Jail Free card right now")]
    [Ephemeral]
    public async Task<Result> UseJailCard(string state)
    {
        await gameControllerService.UseJailCard(state);
        return Result.Success;
    }
    
    [Button("take-card")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.TakeCardOrPayDecision)]
    [Ephemeral]
    public async Task<Result> TakeCard()
    {
        await gameControllerService.TakeCard();
        return Result.Success;
    }
    
    [Button("take-card-pay")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.TakeCardOrPayDecision)]
    [Ephemeral]
    public async Task<Result> TakeCardPay()
    {
        await gameControllerService.TakeCardPay();
        return Result.Success;
    }
    
    [Button("mortgage-transfer-unmortgage")]
    [RequiresPlayer(PlayerType.MatterHead)]
    [NextRequiredAction(GameAction.MortgageTransferDecision)]
    [Ephemeral]
    public async Task<Result> MortgageTransferUnmortgage()
    {
        await gameControllerService.MortgageTransferUnmortgage();
        return Result.Success;
    }
    
    [Button("mortgage-transfer-mortgage")]
    [RequiresPlayer(PlayerType.MatterHead)]
    [NextRequiredAction(GameAction.MortgageTransferDecision)]
    [Ephemeral]
    public async Task<Result> MortgageTransferMortgage()
    {
        await gameControllerService.MortgageTransferMortgage();
        return Result.Success;
    }
    
    [Button("declare-bankruptcy")]
    [RequiresPlayer(PlayerType.Player)]
    [Ephemeral]
    [SuppressInteractionResponse(true)]
    public async Task<Result> DeclareBankruptcy()
    {
        var player = gameState.GetById(ctx.GetUserId());
        if (player.Money >= 0)
        {
            await feedback.SendContextualAsync("You cannot declare bankruptcy because you are not in debt.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return Result.Success;
        }
        
        return await interactionApi.CreateInteractionResponseAsync(ctx.Interaction.ID, ctx.Interaction.Token,
            new InteractionResponse(InteractionCallbackType.Modal, new(new InteractionModalCallbackData(
                CustomIDHelpers.CreateModalID("do-drop"),
                "Declare Bankruptcy",
                [
                    new ActionRowComponent([
                        new TextInputComponent(
                            "drop-response",
                            TextInputStyle.Short,
                            "Do you want to declare bankruptcy? (Y/N)",
                            1,
                            1,
                            true,
                            default,
                            "Y"
                        )])
                ]
            ))));
    }
    
    [Button("auction-drop")]
    [RequiresPlayer(PlayerType.MatterHead)]
    [NextRequiredAction(GameAction.AuctionBid)]
    [Ephemeral]
    public async Task<Result> AuctionDrop()
    {
        await gameControllerService.AuctionDrop();
        return Result.Success;
    }
    
    [Button("auction-bid")]
    [RequiresPlayer(PlayerType.MatterHead)]
    [NextRequiredAction(GameAction.AuctionBid)]
    [Ephemeral]
    public async Task<Result> AuctionBid(int state)
    {
        await gameControllerService.AuctionBid(state);
        return Result.Success;
    }
    
    [Button("auction-bid-other")]
    [RequiresPlayer(PlayerType.MatterHead)]
    [NextRequiredAction(GameAction.AuctionBid)]
    [SuppressInteractionResponse(true)]
    public async Task<Result> AuctionBidOther()
    {
        var auction = (AuctionMatter)gameState.CurrentlyResolvingMatter;
        return await interactionApi.CreateInteractionResponseAsync(ctx.Interaction.ID, ctx.Interaction.Token,
            new InteractionResponse(InteractionCallbackType.Modal, new(new InteractionModalCallbackData(
                CustomIDHelpers.CreateModalID("do-bid-other"),
                $"Bid for {auction.AuctionSpace}",
                [
                    new ActionRowComponent([
                        new TextInputComponent(
                            "bid",
                            TextInputStyle.Short,
                            $"Your bid? (min {(auction.HighestBid + 1).MoneyString(false)})",
                            1,
                            4,
                            true,
                            default,
                            "100"
                        )])
                ]
            ))));
    }
    
    [Button("auction-title-deed")]
    [RequiresPlayer(PlayerType.PlayerOrSpectator)]
    [NextRequiredAction(GameAction.AuctionBid)]
    [Ephemeral]
    public async Task<Result> AuctionTitleDeed()
    {
        var space = ((AuctionMatter)gameState.CurrentlyResolvingMatter).AuctionSpace;
        await feedback.SendContextualAsync($"Title deed for {space}", embeds: new([space.TitleDeedEmbed()]));
        return Result.Success;
    }

    [Modal("do-drop")]
    [RequiresPlayer(PlayerType.Player)]
    public async Task<Result> DoDrop(string dropResponse)
    {
        if (!dropResponse.Equals("y", StringComparison.CurrentCultureIgnoreCase))
        {
            return Result.Success;
        }
        
        var player = gameState.GetById(ctx.GetUserId());
        if (gameState.CurrentlyResolvingMatter.Action == GameAction.AuctionBid)
        {
            await feedback.SendContextualAsync("Please wait for the auction to complete before you drop.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return Result.Success;
        }
        
        if (gameState.CurrentlyResolvingMatter is not DiceRollMatter and not PaymentMatter && gameState.CurrentPlayer == player)
        {
            await feedback.SendContextualAsync("Please complete your turn before you drop.", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return Result.Success;
        }
        
        // Drop the player from the game
        await gameControllerService.DropPlayer(player);
        return Result.Success;
    }
    
    [Modal("do-develop")]
    [RequiresPlayer(PlayerType.Player)]
    public async Task<Result> DoDevelop(int state, int space0 = 0, int space1 = 0, int space2 = 0)
    {
        var player = gameState.GetById(ctx.GetUserId());
        await gameControllerService.DevelopProperties(player, new(state), [space0, space1, space2]);
        return Result.Success;
    }
    
    [Modal("do-bid-other")]
    [RequiresPlayer(PlayerType.MatterHead)]
    [NextRequiredAction(GameAction.AuctionBid)]
    [Ephemeral]
    public async Task<Result> DoBidOther(int bid)
    {
        var auction = (AuctionMatter)gameState.CurrentlyResolvingMatter;
        
        if (bid <= auction.HighestBid)
        {
            await feedback.SendContextualAsync($"You need to bid higher than the highest bid. The highest bid is {auction.HighestBid.MoneyString()}", options: new()
            {
                MessageFlags = new(MessageFlags.Ephemeral)
            });
            return Result.Success;
        }
        
        await gameControllerService.AuctionBid(bid - auction.HighestBid);
        return Result.Success;
    }
}