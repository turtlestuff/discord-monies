using Remora.Results;

namespace DiscordMonies.Extensions;

public class ResultErrorBase : IResultError
{
    // This class is used for cases where we'll pattern match on the error,
    // so the message string is not used.
    public string Message => GetType().Name;
}
