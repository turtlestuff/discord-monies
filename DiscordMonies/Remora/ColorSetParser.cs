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
    public override ValueTask<Result<ColorSet>> TryParseAsync(string token, CancellationToken ct = default)
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return ValueTask.FromResult<Result<ColorSet>>(new NotGameChannelError());
        }

        var group = gameState.Board.GroupNames.Select((name, index) => (name, index))
            .Where(set => token.Equals(set.name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (group.Count == 0)
        {
            return ValueTask.FromResult<Result<ColorSet>>(new NotColorSetError());
        }

        return ValueTask.FromResult(Result<ColorSet>.FromSuccess(new(group.First().index)));
    }
}

public sealed class ColorSetAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider<ColorSet>
{
    public ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = new())
    {
        var res = gameService.GetGameState(ctx) switch
        {
            { } gameState => gameState.Board.GroupNames
                .Where(name => name.StartsWith(userInput, StringComparison.InvariantCultureIgnoreCase))
                .Select(name => new ApplicationCommandOptionChoice(name, name)).ToImmutableList(),
            
            null => []
        };
        return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>(res);
    }
}
