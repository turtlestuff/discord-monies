using DiscordMonies.Remora;

using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace DiscordMonies.Game;

internal sealed class GameCommandPreparationErrorEvent(IFeedbackService feedback) : IPreparationErrorEvent
{
    public async Task<Result> PreparationFailed(IOperationContext context, IResult preparationResult, CancellationToken ct = default)
    {
        for (var inner = preparationResult.Inner; inner != null; inner = inner.Inner)
        {
            if (inner is { Error: DiscordMoniesError { Message: var message } })
            {
                await feedback.SendContextualAsync(message, options: MessageOptions.Ephemeral, ct: ct).AsThrowing();
                break;
            }
        }

        return Result.Success;
    }
}
