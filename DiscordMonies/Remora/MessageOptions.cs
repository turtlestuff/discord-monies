using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Feedback.Messages;

namespace DiscordMonies.Remora;

internal static class MessageOptions
{
    public static FeedbackMessageOptions Ephemeral { get; } = new(MessageFlags: MessageFlags.Ephemeral);
}
