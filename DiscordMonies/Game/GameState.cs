using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

namespace DiscordMonies.Game;

public partial class GameState
{
    public required int GameId { get; init; }

    public required Snowflake ThreadId { get; init; }

    public required Snowflake GuildId { get; init; }


    public required List<Snowflake> PlayerIds { get; init; }

    public required List<GamePlayer> Players { get; init; }

    public int CurrentPlayerIndex { get; set; } = 0;
    public GamePlayer CurrentPlayer => Players[CurrentPlayerIndex];

    public GamePlayer AdvanceTurn()
    {
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
        return Players[CurrentPlayerIndex];
    }

    public required Dictionary<GamePlayer, int> Balances { get; init; }
}

public sealed record GamePlayer(IGuildMember GuildMember, IUser User, string DisplayName, Snowflake Id)
{
    public IGuildMember GuildMember { get; set; } = GuildMember;

    public IUser User { get; set; } = User;

    public string DisplayName { get; set; } = DisplayName;

    public Snowflake Id { get; } = Id;

    public bool Equals(GamePlayer? other)
    {
        return Id == other?.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}