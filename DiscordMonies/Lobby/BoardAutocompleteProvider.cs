using DiscordMonies.Board;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DiscordMonies.Lobby;

public class BoardAutocompleteProvider(BoardService boardService) : IAutocompleteProvider
{
    public ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(IReadOnlyList<IApplicationCommandInteractionDataOption> options, string userInput, CancellationToken ct = new CancellationToken())
    {
        return new(boardService.Boards().Where(x => x.Contains(userInput, StringComparison.InvariantCultureIgnoreCase)).Select(x => new ApplicationCommandOptionChoice(x, x)).ToList());
    }

    public string Identity => "autocomplete::board:name";
    
}