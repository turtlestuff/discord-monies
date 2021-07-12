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
        bool canRoll = true;

        public DiscordMoniesGameInstance(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators) 
            : base(id, client, players, spectators)
        {
            originalPlayerCount = players.Length;
            RegisterCommands();
            currentPlr = Players[Random.Shared.Next(Players.Length)];
            Closing += (_, _) => boardRenderer.Dispose();
        }


        public override async Task Initialize()
        { 
            var asm = GetType().Assembly;

            using var boardStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.board.json")!;
            using var titleStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.titledeeds.json")!;
            using var chestStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.communitychest.json")!;
            using var chanceStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.chance.json")!;

            board = await Board.BoardFromJson(boardStream, titleStream, chestStream, chanceStream);

            for (var i = 0; i < Players.Length; i++) 
            {
                if (!playerStates.TryAdd(Players[i], 
                    new PlayerState(board.StartingMoney, 00, -1, false, BoardRenderer.Colors[i])))
                    throw new Exception("Something very wrong happened Initializing");
            }

            var embed = new EmbedBuilder()
            {
                Title = "First Round",
                Description = $"The game has started! Every player has been given {board.StartingMoney.MoneyString()}\nThe first player is **{currentPlr.Username}**",
                Color = Color.Green
            };
            await SendBoard(Users, embed);
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
            canRoll = true;
            continuousRolls = 0;
            round++;
            var index = Players.IndexOf(currentPlr);
            currentPlr = Players[(index + 1) % Players.Length];
            var embed = new EmbedBuilder()
            {
                Title = "Next Round",
                Description = $"Current Round: {round}\nPlayer: **{currentPlr}** @ " +
                (playerStates[currentPlr].JailStatus == -1 ? playerStates[currentPlr].Position.PositionString() : "Jail"),
                Color = Color.Gold
            };
            await SendBoard(Users, embed);
        }

        async Task SendToJail(IUser player)
        {
            playerStates[player] = playerStates[player] with { JailStatus = 0, Position = board.VisitingJailPosition };
            var embed = new EmbedBuilder()
            {
                Title = "Jail 🚔", //oncoming police car emoji
                Description = $"{player.Username} has been sent to jail.\n" +
                $"They can try rolling doubles 3 times, pay a fine of {board.JailFine.MoneyString()}, or use a Get out of Jail Free card.",
                Color = Color.Red
            }.Build();

            await this.Broadcast("", embed: embed);
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

        async Task<int> MovePlayerRelative(IUser player, int amount)
        {
            var position = (playerStates[player].Position + amount) % board.BoardSpaces.Length;
            var playerMoney = playerStates[player].Money;
            if (position < playerStates[player].Position)
            {
                playerMoney += board.PassGoValue;
                var embed = new EmbedBuilder()
                {
                    Title = "Pass Go Bonus!",
                    Description = $"{player.Username} has gotten {board.PassGoValue.MoneyString()} for passing GO!"
                }.Build();
                await this.Broadcast("", embed: embed);
            }
            playerStates[player] = playerStates[player] with { Position = position, Money = playerMoney};
            return position;
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