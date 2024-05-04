using DiscordMonies.Game;
using Remora.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Results;

namespace DiscordMonies.Remora;

public class RequiresTradeTableCondition(GameService gameService, IInteractionContext ctx) : ICondition<RequiresTradeTableAttribute>
{
    public ValueTask<Result> CheckAsync(RequiresTradeTableAttribute attribute, CancellationToken ct = new CancellationToken())
    {
        if (!ctx.TryGetUserID(out var userId) || !ctx.TryGetChannelID(out var channelId) || gameService.GetGameState(channelId) is not { } gameState)
        {
            return new(new NotGameChannelError());
        }
        
        if (!gameState.PlayerIds.Contains(userId))
        {
            return new(new NotPlayerInGameError());
        }

        var player = gameState.GetById(userId);
        if (player.ConfiguringTradeTable is null)
        {
            return new(new NotPlayerIsConfiguringTradeTableError());
        }

        return new(Result.Success);
    }
}

public sealed class RequiresTradeTableAttribute : ConditionAttribute;