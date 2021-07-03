using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Leisure;

namespace DiscordMoniesGame
{
    public sealed partial class DiscordMoniesGameInstance : GameInstance
    {       
        record UserState (int Money);

        readonly int originalPlayerCount;
        readonly ConcurrentDictionary<IUser, UserState> playerStates = new(DiscordComparers.UserComparer);
        Board board = default!;
        IUser currentUser = default!;

        public DiscordMoniesGameInstance(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators) 
            : base(id, client, players, spectators)
        {
            originalPlayerCount = players.Length;
            RegisterCommands();
        }


        public override async Task Initialize()
        { 
            var asm = GetType().Assembly;

            using var jsonStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.board.json");
            board = await Board.BoardFromJson(jsonStream!);


            foreach (var player in Players)
                if (!playerStates.TryAdd(player, new UserState(board.StartingMoney))) 
                    throw new Exception("Something very wrong happened");

            var embed = new EmbedBuilder()
            {
                Title = "Balance",
                Description = $"The game has started! Every player has been given `Ð{board.StartingMoney:N0}`",
                Color = Color.Green
            }.Build();
            await this.Broadcast("", embed: embed);

        }
        
        public override async Task OnMessage(IUserMessage msg, int pos)
        {
            try
            {
                var msgContent = msg.Content[pos..];
                if (msgContent == "drop")
                {
                    if (currentUser.Equals(msg.Author))
                    {
                        await this.BroadcastTo("You can't `drop` on your own turn.", players: msg.Author);
                        return;
                    }

                    if (playerStates.TryRemove(msg.Author, out _))
                    {
                        var droppedPlayerName = msg.Author.Username;
                        DropPlayer(msg.Author);
                        var embed = new EmbedBuilder()
                        {
                            Title = "Drop",
                            Description = $"**{droppedPlayerName}** has dropped from the game.",
                            Color = Color.Gold
                        }.Build();
                        await this.Broadcast("", embed: embed);
                        return;
                    }

                    await this.BroadcastTo("`drop` failed, please try again.", players: msg.Author);
                    return;
                }

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
                Console.Error.Write(ex);
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