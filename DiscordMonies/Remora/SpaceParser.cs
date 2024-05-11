using System.Collections.Immutable;
using System.Globalization;
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
    public override ValueTask<Result<Space>> TryParseAsync(string token, CancellationToken ct = default)
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return ValueTask.FromResult<Result<Space>>(new NotGameChannelError());
        }
        
        if (token.Length != 2 || !char.IsLetter(token[0]) || !char.IsDigit(token[1]))
        {
            return ValueTask.FromResult<Result<Space>>(new NotGameSpaceError());
        }

        var position = (char.ToLowerInvariant(token[0]) - 'a') * 10 + int.Parse(token.AsSpan(1..), CultureInfo.InvariantCulture);
        if (position < 0 || position > gameState.Board.Spaces.Length)
        {
            return ValueTask.FromResult<Result<Space>>(new NotGameSpaceError());
        }

        return ValueTask.FromResult(Result<Space>.FromSuccess(gameState.Board.Spaces[position]));
    }
}

public sealed class SpaceAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider<Space>
{
    public ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = new())
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>([]);
        }

        return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>(gameState.Board.Spaces
            .Where(space =>
                space.NameWithPositionString.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Select(space => new ApplicationCommandOptionChoice(space.NameWithPositionString, space.PositionCode))
            .ToImmutableList());
    }
}

public sealed class PropertyAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider
{
    public string Identity => "autocomplete::spaces:ownable";
    
    public ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = new CancellationToken())
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>([]);
        }

        return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>(gameState.Board.Spaces
            .Where(space =>
                space.NameWithPositionString.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Where(space => space.Info is PropertySpace)
            .Select(space => new ApplicationCommandOptionChoice(space.NameWithPositionString, space.PositionCode))
            .ToImmutableList());
    }
}

public sealed class TradePropertyAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider
{
    public string Identity => "autocomplete::spaces:trade";
    
    public ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = new CancellationToken())
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>([]);
        }
        
        var player = gameState.GetById(ctx.GetUserId());
        
        var tradeTable = player.ConfiguringTradeTable;
        if (tradeTable is null)
        {
            return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>([]);
        }

        return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>(gameState.Board.Spaces
            .Where(space =>
                space.NameWithPositionString.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Where(space => space.Owner == tradeTable.To)
            .Select(space => new ApplicationCommandOptionChoice(space.NameWithPositionString, space.PositionCode))
            .ToImmutableList());
    }
}

public sealed class OwnedSpaceAutocompleteProvider(GameService gameService, IInteractionContext ctx)
    : IAutocompleteProvider
{
    public string Identity => "autocomplete::spaces:owned";

    public ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = new CancellationToken())
    {
        if (gameService.GetGameState(ctx) is not { } gameState)
        {
            return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>([]);
        }

        var player = gameState.GetById(ctx.GetUserId());

        return ValueTask.FromResult<IReadOnlyList<IApplicationCommandOptionChoice>>(gameState.Board.Spaces
            .Where(space =>
                space.NameWithPositionString.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Where(space => space.Owner == player)
            .Select(space => new ApplicationCommandOptionChoice(space.NameWithPositionString, space.PositionCode))
            .ToImmutableList());
    }
}