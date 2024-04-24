using DiscordMonies.Remora;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DiscordMonies.Game;

[RequiresPlayer(PlayerType.PlayerOrSpectator)]
public class GameCommands(IFeedbackService feedback) : CommandGroup
{
    [Command("balance")]
    public async Task<Result> Balance()
    {
        await feedback.SendContextualAsync("1000");
        return Result.Success;
    }

    [Command("roll")]
    public async Task<IResult> Roll()
    {
        await feedback.SendContextualAsync("Roll");
        return Result.Success;
    }
}
