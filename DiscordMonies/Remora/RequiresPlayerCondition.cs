using DiscordMonies.Game;

using Remora.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Results;

namespace DiscordMonies.Remora;

public sealed class RequiresPlayerCondition(GameService gameService, IInteractionContext ctx) : ICondition<RequiresPlayerAttribute>
{
    public ValueTask<Result> CheckAsync(RequiresPlayerAttribute attribute, CancellationToken ct = default)
    {
        var playerType = attribute.PlayerType;

        // The command must at least run inside a game thread
        if (!ctx.TryGetUserID(out var userId) || !ctx.TryGetChannelID(out var channelId) || gameService.GetGameState(channelId) is not GameState gameState)
        {
            return new(new NotGameChannelError());
        }

        if (playerType == PlayerType.PlayerOrSpectator)
        {
            return new(Result.Success);
        }

        // The command must at least be run by a player in the game
        if (!gameState.PlayerIds.Contains(userId))
        {
            return new(new NotPlayerInGameError());
        }

        if (playerType == PlayerType.Player)
        {
            return new(Result.Success);
        }

        // The command must be run by the current player in the game
        if (gameState.CurrentPlayer.Id != userId)
        {
            return new(new NotCurrentPlayerError());
        }

        return new(Result.Success);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresPlayerAttribute(PlayerType playerType) : ConditionAttribute
{
    public PlayerType PlayerType { get; } = playerType;
}

public enum PlayerType
{
    PlayerOrSpectator, // Anyone in a game channel
    Player,
    CurrentPlayer
}
