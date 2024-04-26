using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Rest.Core;
using Remora.Results;

namespace DiscordMonies.Extensions;

internal static class RemoraExtensions
{
    public static T Get<T>(this IResult<T> result)
    {
        if (result.IsSuccess)
        {
            return result.Entity;
        }

        throw new InvalidOperationException(result.Error.Message) { Data = { ["Error"] = result.Error } };
    }

    public static Task<T> AsThrowing<T>(this Task<Result<T>> task)
    {
        return task.ContinueWith(a => a.Result.Get());
    }

    public static Snowflake GetUserId(this IInteractionContext ctx)
    {
        if (!ctx.TryGetUserID(out var userId))
        {
            throw new InvalidOperationException("Couldn't get interaction user.");
        }

        return userId;
    }
}
