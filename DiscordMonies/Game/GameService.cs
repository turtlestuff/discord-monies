using Remora.Rest.Core;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiscordMonies.Game;

public class GameService
{
    private readonly ConcurrentDictionary<Snowflake, (GameState State, SemaphoreSlim Lock)> games = [];

    public GameState OpenGame(int lobbyId, Snowflake threadId, IEnumerable<Snowflake> players)
    {
        var state = new GameState(lobbyId, threadId, players);
        if (!games.TryAdd(threadId, (state, new SemaphoreSlim(initialCount: 1, maxCount: 1))))
        {
            throw new UnreachableException();
        }

        return state;
    }

    public GameState? GetGameState(Snowflake threadId)
    {
        if (games.TryGetValue(threadId, out var game))
        {
            return game.State;
        }

        return null;
    }

    public async Task AcquireGameLock(GameState state)
    {
        var l = games[state.ThreadId].Lock;
        await l.WaitAsync();
    }

    public void ReleaseGameLock(GameState state)
    {
        var l = games[state.ThreadId].Lock;
        l.Release();
    }
}
