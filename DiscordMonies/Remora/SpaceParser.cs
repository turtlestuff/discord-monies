using System.Collections.Immutable;
using DiscordMonies.Board;
using DiscordMonies.Game;
using Remora.Commands.Parsers;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
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

public sealed class SpaceAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider<Space>
{
    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = new CancellationToken())
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return [];
        }

        return gameState.Board.Spaces.Where(space => space.NameWithPositionString.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Select(space => new ApplicationCommandOptionChoice(space.NameWithPositionString, space.PositionCode)).ToImmutableList();
    }
}

public sealed class PropertyAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider
{
    public string Identity => "autocomplete::spaces:ownable";
    
    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = new CancellationToken())
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return [];
        }

        return gameState.Board.Spaces.Where(space => space.NameWithPositionString.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Where(space => space.Info is PropertySpace)
            .Select(space => new ApplicationCommandOptionChoice(space.NameWithPositionString, space.PositionCode)).ToImmutableList();
    }
}

public sealed class OwnedSpaceAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider
{
    public string Identity => "autocomplete::spaces:owned";

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = new CancellationToken())
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return [];
        }

        var player = gameState.GetById(ctx.GetUserId());

        return gameState.Board.Spaces.Where(space => space.NameWithPositionString.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Where(space => space.Owner == player)
            .Select(space => new ApplicationCommandOptionChoice(space.NameWithPositionString, space.PositionCode)).ToImmutableList();
    }
}