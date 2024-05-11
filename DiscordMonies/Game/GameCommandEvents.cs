using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace DiscordMonies.Game;

internal sealed class GameCommandPreExecutionEvent(GameService service, GameStateInjector injector, IInteractionContext ctx) : IPreExecutionEvent
{
    public async Task<Result> BeforeExecutionAsync(ICommandContext context, CancellationToken ct = default)
    {
        if (context is not InteractionCommandContext)
        {
            return Result.Success;
        }

        if (ctx.TryGetChannelID(out var channelId) && service.GetGameState(channelId) is { } gameState)
        {
            injector.GameState = gameState;
            await service.AcquireGameLock(gameState);
        }

        return Result.Success;
    }
}

internal sealed class GameCommandPostExecutionEvent(GameService service, IInteractionContext ctx) : IPostExecutionEvent
{
    public Task<Result> AfterExecutionAsync(ICommandContext context, IResult commandResult, CancellationToken ct = default)
    {
        if (context is not InteractionCommandContext)
        {
            return Task.FromResult(Result.Success);
        }

        // Use the same conditions as the pre-execution event so we don't end up with an unbalanced count on the semaphore
        if (ctx.TryGetChannelID(out var channelId) && service.GetGameState(channelId) is { } gameState)
        {
            service.ReleaseGameLock(gameState);
        }

        return Task.FromResult(
            commandResult.IsSuccess
            ? Result.Success
            : Result.FromError(new CommandFailedError(context, commandResult.Error), commandResult));
    }
}

public class CommandFailedError(ICommandContext context, IResultError error) : IResultError
{
    public string Message
    {
        get
        {
            var commandName = context.Command.Command.Node.Key;
            return error switch
            {
                ExceptionError { Exception: var ex } => $"The command '{commandName}' failed with an exception:{Environment.NewLine}{ex}",
                _ => $"The command '{commandName}' failed:{Environment.NewLine}{error.Message}"
            };
        }
    }
}
