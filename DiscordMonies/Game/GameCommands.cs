using DiscordMonies.Remora;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;
using Remora.Results;

namespace DiscordMonies.Game;

public class GameCommands(GameState gameState, IFeedbackService feedback, IInteractionCommandContext ctx) : CommandGroup
{
    [Command("balance")]
    [RequiresPlayer(PlayerType.PlayerOrSpectator)]
    [Ephemeral]
    public async Task<Result> Balance(GamePlayer? player = null)
    {
        player ??= gameState.CurrentPlayer;
        var state = gameState.PlayerStates[player];
        await feedback.SendContextualAsync($"{player.DisplayName}'s balance: {Formatting.MoneyString(state.Money)}");
        return Result.Success;
    }

    [Command("roll")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    public async Task<Result> Roll()
    {
        await feedback.SendContextualAsync("Roll");
        gameState.AdvanceTurn();
        return Result.Success;
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
