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

public sealed class ColorSetParser(GameService gameService, IInteractionContext ctx) : AbstractTypeParser<ColorSet>
{
    public override async ValueTask<Result<ColorSet>> TryParseAsync(string token, CancellationToken ct = default)
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return new NotGameChannelError();
        }

        var group = gameState.Board.GroupNames.Select((name, index) => (name, index))
            .Where(set => token.Equals(set.name, StringComparison.CurrentCultureIgnoreCase)).ToList();

        if (group.Count == 0)
        {
            return new NotColorSetError();
        }

        return Result<ColorSet>.FromSuccess(new(group.First().index));
    }
}

public sealed class ColorSetAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider<ColorSet>
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

        return gameState.Board.GroupNames.Select(name => new ApplicationCommandOptionChoice(name, name)).ToImmutableList();
    }
}