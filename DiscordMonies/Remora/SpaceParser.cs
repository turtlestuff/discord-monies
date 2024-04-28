using DiscordMonies.Board;
using DiscordMonies.Game;
using Remora.Commands.Parsers;
using Remora.Discord.Commands.Contexts;
using Remora.Results;

namespace DiscordMonies.Remora;

public sealed class SpaceParser(GameService gameService, IInteractionContext ctx) : AbstractTypeParser<Space>
{
    public override async ValueTask<Result<Space>> TryParseAsync(string token, CancellationToken ct = default)
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return new NotGameChannelError();
        }
        
        if (token.Length != 2 || !char.IsLetter(token[0]) || !char.IsDigit(token[1]))
        {
            return new NotGameSpaceError();
        }

        var position = (char.ToLowerInvariant(token[0]) - 'a') * 10 + int.Parse(token[1..]);
        if (position < 0 || position > gameState.Board.Spaces.Length)
        {
            return new NotGameSpaceError();
        }

        return Result<Space>.FromSuccess(gameState.Board.Spaces[position]);
    }
}