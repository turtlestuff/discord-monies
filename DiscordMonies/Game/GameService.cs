using DiscordMonies.Extensions;

using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Rest.Core;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiscordMonies.Game;

public class GameService(IDiscordRestGuildAPI guildApi)
{
    private readonly ConcurrentDictionary<Snowflake, (GameState State, SemaphoreSlim Lock)> games = [];

    public void OpenGame(GameState gameState)
    {
        if (!games.TryAdd(gameState.ThreadId, (gameState, new SemaphoreSlim(initialCount: 1, maxCount: 1))))
        {
            throw new UnreachableException();
        }
    }

    public GameState? GetGameState(Snowflake threadId)
    {
        if (games.TryGetValue(threadId, out var game))
        {
            return game.State;
        }

        return null;
    }

    public GameState? GetGameState(IInteractionContext ctx)
    {
        if (!ctx.TryGetChannelID(out var channelId) || GetGameState(channelId) is not { } state)
        {
            return null;
        }

        return state;
    }

    public async Task UpdateGameState(GameState gameState)
    {
        // Since we enabled caching, these API calls will be served from cache when possible
        foreach (var player in gameState.Players)
        {
            var member = await guildApi.GetGuildMemberAsync(gameState.GuildId, player.Id).AsThrowing();
            var user = member.User.Value;

            player.GuildMember = member;
            player.User = user;

            player.DisplayName = member.Nickname.OrDefault() ?? user.Username;
        }
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
