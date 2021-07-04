using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Leisure
{
    static partial class Program
    {
        static string GetRandomGamePrefix() => InstalledGames.Keys.ElementAt(Random.Shared.Next(0, InstalledGames.Keys.Count));

        #region Parsing Helpers
        
        static async Task ParseGuildMessage(SocketUserMessage msg)
        {
            // Is this a leisure command?
            var splits = msg.Content.Split(":");
            var start = splits[0];
            
            // If someone doesn't understand what <game> means in the help, they'll figure out the hard way
            if (start == "leisure" || start == "<game>")
            {
                await ParseLeisureCommand(msg, splits[0].Length + 1);
            }

            // Search through installed InstalledGames to find one with the prefix. If one is found, try and parse the command
            if (InstalledGames.TryGetValue(start, out var game))
            {
                await ParseGameCommand(game, msg, splits[0].Length + 1);
            }
        }
        
        static Task ParseLeisureCommand(SocketUserMessage msg, int pos) =>
            msg.Content[pos..] switch
            {
                "help" => SendHelp(msg),
                "games" => SendGames(msg),
                var s when s == "ping" || s == "about" => SendAbout(msg),
                var gameName when InstalledGames.TryGetValue(gameName, out var game) => SendGame(game, msg),
                var s when s == "join" || s == "spectate" || s == "close" => msg.Channel.SendMessageAsync(
                    $"Use the prefix of a game (e.g. `{GetRandomGamePrefix()}`) to {msg.Content[pos..]} a game."),
                "<game>" => msg.Channel.SendMessageAsync(
                    $"Use the prefix of a game (e.g. `leisure:{GetRandomGamePrefix()}`) to get more information about a game."),
                _ => Task.CompletedTask
            };

        static async Task ParseGameCommand(GameInfo game, SocketUserMessage msg, int pos)
        {
            switch (msg.Content[pos..])
            {
                case "join":
                    if (Lobbies.TryGetValue(msg.Channel, out var startingGame))
                        await JoinExistingLobby(game, msg, startingGame, false);
                    else
                        await StartNewLobby(game, msg);
                    return;
                case "spectate":
                    if (game.SupportsSpectators && Lobbies.TryGetValue(msg.Channel, out startingGame)) 
                        await JoinExistingLobby(game, msg, startingGame, true);
                    return;
                case "close" when Lobbies.TryGetValue(msg.Channel, out var newGame)
                                  && newGame.StartingUser == msg.Author:
                    await CloseLobby(game, msg.Channel, newGame);
                    return;
                default:
                    return;
            }
        }
        
        #endregion

        #region Lobby Helpers
        
        static async Task CloseLobby(GameInfo game, ISocketMessageChannel channel, GameLobby lobby)
        {
            if (!game.IsValidPlayerCount(lobby.Players.Count))
            {
                await channel.SendMessageAsync($@"**Lobby Closed**
The lobby was closed, but an invalid amount of players were in the lobby. {game.Name} requires: {game.PlayerCountDescription}");
            }
            else
            {
                lobby.Closed = true;
                await channel.SendMessageAsync($"Lobby #{lobby.Id} has closed and the game is now starting.");
                var newGame = game.CreateGame(lobby.Id, Client, lobby.Players.ToImmutable(), lobby.Spectators.ToImmutable());

                foreach (var p in lobby.Users)
                {
                    var gc = PlayingUsers.GetOrAdd(p, _ => new GameCollection());
                    gc.Games.TryAdd(newGame.Id, newGame);
                    gc.CurrentGame = newGame;
                }

                newGame.Closing += CloseGame;
                newGame.UserDropping += DropUser;
                
                await newGame.Initialize();
            }


            Lobbies.TryRemove(channel, out _);
        }
        
        static async Task JoinExistingLobby(GameInfo game, SocketUserMessage msg, GameLobby gameLobby, bool spectating)
        {
            if (gameLobby.Users.Contains(msg.Author, DiscordComparers.UserComparer))
                await msg.Channel.SendMessageAsync($"{msg.Author.Mention}, You have already joined this lobby.");
            else
            {
                (spectating ? (game.SupportsSpectators ? gameLobby.Spectators : gameLobby.Players) : gameLobby.Players).Add(msg.Author);
                await msg.Channel.SendMessageAsync($"**{msg.Author}** has joined the lobby.");
            }
        }

        static async Task StartNewLobby(GameInfo game, SocketUserMessage msg)
        {
            var number = Interlocked.Add(ref GameCount, 1);
            var lobby = new GameLobby(number, msg.Author, msg.Channel, game);
            Lobbies.TryAdd(msg.Channel, lobby);
            
            var builder = new EmbedBuilder
            {
                Color = LeisureColor,
                ThumbnailUrl = game.IconUrl,
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = msg.Author.GetAvatarUrl(),
                    Name = $"Ready to play {game.Name}?"
                },
                Description = $@"
{msg.Author.Mention} has opened lobby #{number}.
• To join this game, type `{game.Prefix}:join`
• To spectate this game, type `{game.Prefix}:spectate`
• Once everyone has joined the lobby, {msg.Author.Mention} can use `{game.Prefix}:close` to start playing",
                Footer = new EmbedFooterBuilder
                {
                    Text = "This lobby will close after five minutes."
                }
            }.WithCurrentTimestamp();
            
            await msg.Channel.SendMessageAsync("", embed: builder.Build());
            _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(async t =>
            {
                if (lobby.Closed) return;
                await CloseLobby(game, msg.Channel, lobby);
            });
        }
        
        #endregion
        
        #region Embed Helpers 
        
        static async Task SendAbout(SocketUserMessage msg)
        {
            var offset = DateTime.Now;
            var edit = await msg.Channel.SendMessageAsync("**Pong!** Calculating ping... 📶");
            await edit.ModifyAsync(e =>
            {
                var v = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
                        typeof(Program).Assembly.GetName().Version?.ToString();
                
                e.Content = "";
                e.Embed = new EmbedBuilder
                {
                    Color = LeisureColor,
                    Author = new EmbedAuthorBuilder
                    {
                        IconUrl = Client.CurrentUser.GetAvatarUrl(),
                        Name = $"Leisure v{v}",
                        Url = "https://github.com/turtlestuff/leisure-net",
                    },
                    Fields =
                    {
                        new EmbedFieldBuilder
                        {
                            Name = "Statistics",
                            Value = $@"**Heartbeat:** {Client.Latency}ms
**API Latency:** {(DateTime.Now - offset).Milliseconds}ms
**Working Set:** {Process.GetCurrentProcess().WorkingSet64 / 1E6:F} MB
**.NET Version:** {RuntimeInformation.FrameworkDescription}
**Operating System:** {RuntimeInformation.OSDescription}"
                        },
                        new EmbedFieldBuilder
                        {
                            Name = "About",
                            Value = @"Leisure.NET is based off of the [original Leisure project](https://github.com/vicr123/leisurebot), written in JavaScript.
Source code for Leisure.NET is available on [GitHub](https://github.com/turtlestuff/leisure-net), licenced under the GNU Lesser General Public License."
                        }
                    },
                    Footer = new EmbedFooterBuilder
                    {
                        Text = "Thanks for playing with Leisure!"
                    }
                }.Build();
            });
        }

        static async Task SendHelp(SocketUserMessage msg)
        {
            var builder = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = Client.CurrentUser.GetAvatarUrl(),
                    Name = "Leisure Help"
                },
                Color = LeisureColor,
                Fields =
                {
                    new EmbedFieldBuilder
                    {
                        Name = "Leisure Commands",
                        Value = @"**leisure:ping** - Makes sure the bot is online
**leisure:help** - Gets this message
**leisure:games** - Gets the list of the available games
**leisure:<game>** - Gets information about the specified game"
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Game Commands",
                        Value = @"**<game>:join** - Starts or joins a lobby in the current channel
**<game>:spectate** - Adds you as a spectator to the lobby in the current channel
**<game>:close** - If you created the lobby in the current channel, closes it and starts the game"
                    }
                },
                Footer = new EmbedFooterBuilder
                {
                    Text = "Replace <game> with the game prefix of your choice. To find out about the available game and game prefixes, type leisure:help"
                }
            };

            await msg.Channel.SendMessageAsync("", embed: builder.Build());
        }

        static async Task SendGame(GameInfo game, SocketUserMessage msg)
        {
            var builder = new EmbedBuilder
            {
                Color = LeisureColor,
                ThumbnailUrl = game.IconUrl,
                Title = $"**{game.Name}** ({game.Prefix}:)",
                Description = $@"{game.Description}

**Author(s)**: {game.Author}
**Version:** {game.Version}
**Player Requirements:** {game.PlayerCountDescription}"
            };

            await msg.Channel.SendMessageAsync("", embed: builder.Build());
        }

        static async Task SendGames(SocketUserMessage msg)
        {
            var builder = new EmbedBuilder
            {
                Title = "Available Games",
                Color = LeisureColor,
                Description = string.Join("\n", InstalledGames.Values.Select(game => $"**{game.Name}** ({game.Prefix}:)"))
            };

            await msg.Channel.SendMessageAsync("", embed: builder.Build());
        }
        
        #endregion
    }
}