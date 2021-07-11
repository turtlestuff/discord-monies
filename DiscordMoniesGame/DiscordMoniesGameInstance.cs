using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Leisure;

namespace DiscordMoniesGame
{
    public sealed partial class DiscordMoniesGameInstance : GameInstance
    {       
        public record PlayerState(int Money, int Position, int JailStatus, bool GetOutOfJailCard, System.Drawing.Color Color);
        // JailStatus: -1 if out of jail, 0-3 if in jail, counting the consecutive turns of double attempts.

        readonly int originalPlayerCount;
        readonly ConcurrentDictionary<IUser, PlayerState> playerStates = new(DiscordComparers.UserComparer);
        Board board = default!;
        IUser currentPlr;
        int round = 1;
        int continuousRolls;
        readonly BoardRenderer boardRenderer = new();

        public DiscordMoniesGameInstance(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators) 
            : base(id, client, players, spectators)
        {
            originalPlayerCount = players.Length;
            RegisterCommands();
            currentPlr = Players[Random.Shared.Next(Players.Length)];
        }


        public override async Task Initialize()
        { 
            var asm = GetType().Assembly;

            using var jsonStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.board.json")!;
            board = await Board.BoardFromJson(jsonStream);

            for (var i = 0; i < Players.Length; i++) 
            {
                if (!playerStates.TryAdd(Players[i], 
                    new PlayerState(board.StartingMoney, 00, -1, false, BoardRenderer.Colors[i])))
                    throw new Exception("Something very wrong happened Initializing");
            }

            var embed = new EmbedBuilder()
            {
                Title = "Balance",
                Description = $"The game has started! Every player has been given `Ð{board.StartingMoney:N0}`\nThe first player is **{currentPlr.Username}**",
                Color = Color.Green
            }.Build();
            await this.Broadcast("", embed: embed);
            await SendBoard(Users);
        }

        public override async Task OnMessage(IUserMessage msg, int pos)
        {
            try
            {
                var msgContent = msg.Content[pos..];

                if (await TryHandleCommand(msgContent, msg))
                    return;

                //Message hasn't matched any commands, proceed to send as chat message...
                var chatMessage =
                    $"**{msg.Author.Username}{(Spectators.Contains(msg.Author, DiscordComparers.UserComparer) ? " (Spectator)" : "")}**: {msgContent}";
                if (chatMessage.Length <= 2000)
                {
                    // ...except if it's too large!
                    await this.BroadcastExcluding(chatMessage, exclude: msg.Author);
                    await msg.AddReactionAsync(new Emoji("💬"));
                }
            }
            catch (Exception ex)
            {
                await this.BroadcastTo($"Command failed: {ex.Message}", players: msg.Author);
                Console.Error.WriteLine(ex);
            }
        }

        async Task AdvanceRound()
        {
            continuousRolls = 0;
            round++;
            var index = Players.IndexOf(currentPlr);
            currentPlr = Players[(index + 1) % Players.Length];
            var embed = new EmbedBuilder()
            {
                Title = "Next Round",
                Description = $"Current Round: {round}\nPlayer: **{currentPlr}** @ `{Board.PositionString(playerStates[currentPlr].Position)}`",
                Color = Color.Green
            };
            await SendBoard(Users, embed);
        }

        async Task SendBoard(IEnumerable<IUser> users, EmbedBuilder? embed = null)
        {
            using var bmp = boardRenderer.Render(Players, playerStates, board);
            using var memStr = new MemoryStream();
            bmp.Save(memStr, System.Drawing.Imaging.ImageFormat.Png);
            foreach (var u in users)
            {
                memStr.Position = 0;
                using var clone = new MemoryStream();
                await memStr.CopyToAsync(clone);
                clone.Position = 0;
                if (embed is not null)
                {
                    embed.ImageUrl = "attachment://board.png";
                    await u.SendFileAsync(clone, "board.png", embed: embed.Build());
                }
                else
                {
                    await u.SendFileAsync(clone, "board.png");
                }
            }
        }

        void Close()
        {
            OnClosing();
        }
        
        void DropPlayer(IUser player)
        {
            OnDroppingUser(player);
        }
    }
}