using DiscordMonies.Remora;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace DiscordMonies.Game;

public class GameCommands(GameState gameState, IFeedbackService feedback, IInteractionCommandContext ctx, GameControllerService gameControllerService) : CommandGroup
{
    [Command("balance")]
    [RequiresPlayer(PlayerType.PlayerOrSpectator)]
    [Ephemeral]
    public async Task<Result> Balance(GamePlayer? player = null)
    {
        player ??= gameState.GetById(ctx.GetUserId());
        await feedback.SendContextualAsync($"{player}'s balance: {player.Money.MoneyString()}");
        return Result.Success;
    }

    [Command("roll")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    [NextRequiredAction(GameAction.Roll, ErrorMessage = "You can't roll now.")]
    public async Task<Result> Roll()
    {
        return await gameControllerService.RollDie();
    }

    [Command("nudge")]
    [RequiresPlayer(PlayerType.Player)]
    public async Task<Result> Nudge()
    {
        var current = gameState.CurrentPlayer;
        await feedback.SendContextualAsync(
            $"{Mention.User(current.Id)}, {gameState.GetById(ctx.GetUserId()).DisplayName} wished to remind you that it is your turn to play by giving you a gentle nudge. *Nudge!*");
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
}