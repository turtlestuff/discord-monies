using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Caching.Services;
using Remora.Rest.Core;

namespace DiscordMonies.Game;

public partial class GameState
{
    public GameState(int gameId, Snowflake threadId, IEnumerable<Snowflake> playerList)
    {
        GameId = gameId;
        ThreadId = threadId;

        playerIds = playerList.ToList();
    }

    public int GameId { get; }

    public Snowflake ThreadId { get; }

    public IReadOnlyList<Snowflake> PlayerIds => playerIds;

    private readonly List<Snowflake> playerIds;

    public IReadOnlyList<GamePlayer> Players => players;

    private List<GamePlayer> players = [];

    public void UpdatePlayers(List<GamePlayer> players)
    {
        this.players = players;
    }
}

public sealed record GamePlayer(IGuildMember GuildMember, IUser User, string DisplayName, Snowflake Id)
{
    public bool Equals(GamePlayer? other)
    {
        return Id == other?.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}