using DiscordMonies.Remora;

using Remora.Rest.Core;
using Remora.Results;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiscordMonies.Lobby;

public sealed class LobbyService
{
    private sealed class Lobby(int lobbyId, Snowflake guildId, Snowflake creatorId)
    {
        public int LobbyId { get; } = lobbyId;

        public Snowflake GuildId { get; } = guildId;

        public Snowflake CreatorId { get; } = creatorId;

        public List<Snowflake> PlayerIds { get; } = [creatorId];
    }

    private readonly ConcurrentDictionary<int, Lobby> lobbies = new();

    private int currentLobbyId;

    public int OpenLobby(Snowflake guildId, Snowflake creatorId)
    {
        var lobbyId = Interlocked.Increment(ref currentLobbyId);
        if (!lobbies.TryAdd(lobbyId, new Lobby(lobbyId, guildId, creatorId)))
        {
            throw new UnreachableException();
        }

        return lobbyId;
    }

    public IResult JoinLobby(int lobbyId, Snowflake playerId)
    {
        if (!lobbies.TryGetValue(lobbyId, out var lobby))
        {
            return Result.FromError(new LobbyIsClosed());
        }

        lock (lobby)
        {
            if (lobby.PlayerIds.Contains(playerId))
            {
                return Result.FromError(new UserCannotJoinDropClose());
            }

            lobby.PlayerIds.Add(playerId);
            return Result.Success;
        }
    }

    public IResult DropLobby(int lobbyId, Snowflake playerId)
    {
        if (!lobbies.TryGetValue(lobbyId, out var lobby))
        {
            return Result.FromError(new LobbyIsClosed());
        }

        lock (lobby)
        {
            if (!lobby.PlayerIds.Remove(playerId))
            {
                return Result.FromError(new UserCannotJoinDropClose());
            }

            return Result.Success;
        }
    }

    public IResult<ClosedLobby> CloseLobby(int lobbyId, Snowflake playerId)
    {
        if (!lobbies.TryGetValue(lobbyId, out var lobby))
        {
            return Result<ClosedLobby>.FromError(new LobbyIsClosed());
        }

        if (lobby.CreatorId != playerId)
        {
            return Result<ClosedLobby>.FromError(new UserCannotJoinDropClose());
        }
        
        if (!lobbies.TryRemove(lobbyId, out _))
        {
            return Result<ClosedLobby>.FromError(new LobbyIsClosed());
        }

        var result = new ClosedLobby(lobby.LobbyId, lobby.GuildId, lobby.PlayerIds);
        return Result<ClosedLobby>.FromSuccess(result);
    }
}

public sealed record ClosedLobby(int LobbyId, Snowflake GuildId, List<Snowflake> Players);

public class LobbyIsClosed : ResultErrorBase;

public class UserCannotJoinDropClose : ResultErrorBase;
