using DiscordMonies.Extensions;
using Remora.Rest.Core;
using Remora.Results;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace DiscordMonies.Lobby;

public sealed class LobbyService
{
    private sealed class Lobby(int lobbyId, Snowflake creatorId)
    {
        public int LobbyId { get; } = lobbyId;

        public Snowflake CreatorId { get; } = creatorId;

        private readonly List<Snowflake> playerIds = [];

        public bool JoinPlayer(Snowflake playerId)
        {
            if (playerIds.Contains(playerId))
            {
                return false;
            }

            playerIds.Add(playerId);
            return true;
        }

        public bool DropPlayer(Snowflake playerId)
        {
            return playerIds.Remove(playerId);
        }

        public ImmutableArray<Snowflake> GetPlayerIds() => playerIds.ToImmutableArray();
    }

    private readonly ConcurrentDictionary<int, Lobby> lobbies = new();

    private int currentLobbyId;

    public int OpenLobby(Snowflake creatorId)
    {
        var lobbyId = Interlocked.Increment(ref currentLobbyId);
        if (!lobbies.TryAdd(lobbyId, new Lobby(lobbyId, creatorId)))
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
            if (!lobby.JoinPlayer(playerId))
            {
                return Result.FromError(new UserCannotJoinDropClose());
            }

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
            if (!lobby.DropPlayer(playerId))
            {
                return Result.FromError(new UserCannotJoinDropClose());
            }

            return Result.Success;
        }
    }

    public IResult<ClosedLobby> CloseLobby(int lobbyId, Snowflake playerId)
    {
        if (!lobbies.TryRemove(lobbyId, out var lobby))
        {
            return Result<ClosedLobby>.FromError(new LobbyIsClosed());
        }

        if (lobby.CreatorId != playerId)
        {
            return Result<ClosedLobby>.FromError(new UserCannotJoinDropClose());
        }

        var result = new ClosedLobby(lobby.LobbyId, lobby.GetPlayerIds());
        return Result<ClosedLobby>.FromSuccess(result);
    }
}

public sealed record ClosedLobby(int LobbyId, ImmutableArray<Snowflake> Players);

public class LobbyIsClosed : ResultErrorBase;

public class UserCannotJoinDropClose : ResultErrorBase;
