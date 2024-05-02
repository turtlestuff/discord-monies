using DiscordMonies.Game;

using Remora.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Results;

namespace DiscordMonies.Remora;

public sealed class NextRequiredActionCondition(GameService gameService, IInteractionContext ctx) : ICondition<NextRequiredActionAttribute>
{
    public ValueTask<Result> CheckAsync(NextRequiredActionAttribute attribute, CancellationToken ct = default)
    {
        var actions = attribute.Actions;

        // The command must at least run inside a game thread
        if (!ctx.TryGetChannelID(out var channelId) || gameService.GetGameState(channelId) is not GameState gameState)
        {
            return new(new NotGameChannelError());
        }

        if (actions.Contains(gameState.CurrentlyResolvingMatter.Action))
        {
            return new(Result.Success);
        }

        return new(new DiscordMoniesError(attribute.ErrorMessage));
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class NextRequiredActionAttribute(params GameAction[] actions) : ConditionAttribute
{
    public GameAction[] Actions { get; } = actions;

    public string ErrorMessage { get; set; } = "You can't do this right now.";
}
