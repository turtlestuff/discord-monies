using Remora.Results;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordMonies.Extensions;

internal static class ResultExtensions
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

}
