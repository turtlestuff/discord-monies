using System.ComponentModel;
using DiscordMonies.Board;
using DiscordMonies.Extensions;
using DiscordMonies.Remora;

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

public class GameCommands(GameState gameState, IFeedbackService feedback, IInteractionCommandContext ctx, GameControllerService gameControllerService, IDiscordRestInteractionAPI interactionApi) : CommandGroup
{
    [Command("drop")]
    [Description("Leave the game")]
    [RequiresPlayer(PlayerType.Player)]
    [Ephemeral]
    [SuppressInteractionResponse(true)]
    public async Task<Result> Drop()
    {
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
    public async Task<Result> Nudge()
    {
        var current = gameState.CurrentPlayer;
        await feedback.SendContextualAsync(
            $"{Mention.User(current.Id)}, {gameState.GetById(ctx.GetUserId()).DisplayName} wished to remind you that it is your turn to play by giving you a gentle nudge. *Nudge!*");
        return Result.Success;
    }

    [Command("deed")]
    [Description("View the title deed for a property")]
    [RequiresPlayer(PlayerType.PlayerOrSpectator)]
    [Ephemeral]
    public async Task<Result> Deed([Description("The space to view the title deed for")] Space space)
    {
        await feedback.SendContextualAsync($"Title deed for {space}", embeds: new([space.TitleDeedEmbed()]));
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

public class GameInteractions(GameControllerService gameControllerService) : InteractionGroup
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

    [Modal("do-drop")]
    [RequiresPlayer(PlayerType.Player)]
    public async Task<Result> DoDrop(string dropResponse)
    {
        if (!dropResponse.Equals("y", StringComparison.CurrentCultureIgnoreCase))
        {
            return Result.Success;
        }
        
        // Drop the player from the game
        return Result.Success;
    }
}