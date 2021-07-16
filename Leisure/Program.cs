using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Leisure
{
    static partial class Program
    {
        public static DiscordSocketClient Client = default!;

        public static ConcurrentDictionary<IUser, GameCollection> PlayingUsers = new(DiscordComparers.UserComparer);

        public static Dictionary<string, GameInfo> InstalledGames = default!;

        public static ConcurrentDictionary<IMessageChannel, GameLobby> Lobbies = new(DiscordComparers.ChannelComparer);

        public static int GameCount;
        
        public static Color LeisureColor = new(160, 28, 195);

        static readonly System.Drawing.Bitmap? dummy = new(1, 1);

        static async Task Main()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string token;
            if (!File.Exists("token.txt"))
            {
                Console.WriteLine("\"token.txt\" doesn't exist. Enter token:");
                token = Console.ReadLine() ?? throw new Exception("");
                Console.WriteLine("Create token.txt? [y/n]");
                if (Console.ReadKey().Key != ConsoleKey.N)
                {
                    await using var file = File.CreateText("token.txt");
                    file.Write(token);
                }
            }
            else
            {
                token = File.ReadAllText("token.txt");
            }

            var gameInfos = new Dictionary<string, GameInfo>();

            // Find all the InstalledGames. The game DLL's name has to currently end in "Game". 
            // TODO: Isolate each game to its own folder
            foreach (var asm in Directory.GetFiles("Games", "*Game.dll", SearchOption.AllDirectories))
            {
                try
                {
                    Console.WriteLine("Loading " + asm);

                    // Load the assembly
                    // TODO: Assembly unloadability
                    var assembly = Assembly.LoadFrom(asm);

                    // Find every class that implements GameInfo and make a new instance of it.
                    foreach (var (prefix, info) in from t in assembly.ExportedTypes
                        where t.IsSubclassOf(typeof(GameInfo))
                        let info = (GameInfo) Activator.CreateInstance(t)!
                        select (info.Prefix, GameInfo: info))
                    {
                        gameInfos.Add(prefix, info);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error loading assembly at {0}: {1}", asm, ex);
                }
            }

            InstalledGames = gameInfos;

            Client = new DiscordSocketClient();
            Client.MessageReceived += ClientOnMessageReceived;
            Client.Log += msg =>
            {
                Console.WriteLine(msg.ToString());
                return Task.CompletedTask;
            };

            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        static async Task ClientOnMessageReceived(SocketMessage arg)
        {
            // Not a message sent by a user
            if (arg is not SocketUserMessage msg)
                return;

            if (msg.Channel is IGuildChannel)
            {
                // We are in a discord server. Try to start new game
                await ParseGuildMessage(msg);
                return;
            }

            if (msg.Author != Client.CurrentUser && PlayingUsers.ContainsKey(msg.Author))
            {
                try
                {
                    await ParseDmMessage(msg);
                }
                catch (Exception ex)
                {
                    await msg.Author.SendMessageAsync(ex.Message);
                }
            }
        }
    }
}