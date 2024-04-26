using DiscordMonies.Extensions;
using DiscordMonies.Remora;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;
using Remora.Results;

namespace DiscordMonies.Game;

[RequiresPlayer(PlayerType.PlayerOrSpectator)]
public class GameCommands(GameState gameState, IFeedbackService feedback, IInteractionCommandContext ctx) : CommandGroup
{
    [Command("balance")]
    [Ephemeral]
    public async Task<Result> Balance(IUser? user = null)
    {
        var player = gameState.GetById(user?.ID ?? ctx.GetUserId());
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
    public async Task<Result> Nudge()
    {
        var current = gameState.CurrentPlayer;
        await feedback.SendContextualAsync(
            $"{Mention.User(current.Id)}, {gameState.GetById(ctx.GetUserId()).DisplayName} wished to remind you that it is your turn to play by giving you a gentle nudge. *Nudge!*");
        return Result.Success;
    }
}
