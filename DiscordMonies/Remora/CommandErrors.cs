using Remora.Discord.Extensions.Formatting;
using Remora.Results;

namespace DiscordMonies.Remora;

internal record DiscordMoniesError(string Message) : ResultError(Message);

internal sealed record NotGameChannelError() : DiscordMoniesError("You can't use this command outside of a game.");

internal sealed record NotGamePlayerError() : DiscordMoniesError("You must specify a player in this game.");

internal sealed record NotPlayerInGameError() : DiscordMoniesError("You can only use this command if you're a player in this game.");

internal sealed record NotCurrentPlayerError() : DiscordMoniesError("You can only use this command if it's your turn.");

internal sealed record NotGameSpaceError() : DiscordMoniesError("That's not a valid space on the board.");

internal sealed record NotColorSetError() : DiscordMoniesError("That's not a valid color set.");

// TODO: Replace error message with a mention of the slash command
internal sealed record NotPlayerIsConfiguringTradeTableError() : DiscordMoniesError("You have no open trade table. Open a trade table using the /trade open command.");