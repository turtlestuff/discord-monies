using DiscordMonies.Game;

using Remora.Commands.Parsers;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;

namespace DiscordMonies.Remora;

internal sealed record NotGameChannelError() : ResultError("You can't use this command outside of a game.");

internal sealed record NotGamePlayerError() : ResultError("You must specify a player in this game.");

public sealed class GamePlayerParser(GameService gameService, IInteractionContext ctx, ITypeParser<IUser> userParser) : AbstractTypeParser<GamePlayer>
{
    public override async ValueTask<Result<GamePlayer>> TryParseAsync(string token, CancellationToken ct = default)
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return new NotGameChannelError();
        }

        var userResult = await userParser.TryParseAsync(token, ct);
        if (!userResult.IsSuccess)
        {
            return Result<GamePlayer>.FromError(userResult.Error);
        }

        if (gameState.TryGetById(userResult.Entity) is not { } player)
        {
            return Result<GamePlayer>.FromError(new NotGamePlayerError());
        }

        return Result<GamePlayer>.FromSuccess(player);
    }
}