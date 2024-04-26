using DiscordMonies.Remora;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DiscordMonies.Game;

[RequiresPlayer(PlayerType.PlayerOrSpectator)]
public class GameCommands(GameState state, IFeedbackService feedback) : CommandGroup
{
    [Command("balance")]
    public async Task<Result> Balance()
    {
        var bal = state.Balances[state.CurrentPlayer];
        await feedback.SendContextualAsync(bal.ToString());
        return Result.Success;
    }

    [Command("roll")]
    [RequiresPlayer(PlayerType.CurrentPlayer)]
    public async Task<IResult> Roll()
    {
        await feedback.SendContextualAsync("Roll");
        state.AdvanceTurn();
        return Result.Success;
    }
}
