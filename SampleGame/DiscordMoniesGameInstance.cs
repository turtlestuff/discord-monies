using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Leisure;

namespace DiscordMoniesGame
{
    public sealed class DiscordMoniesGameInstance : GameInstance
    {       
        record UserState (int Money);

        readonly int originalPlayerCount;
        readonly ConcurrentDictionary<IUser, UserState> userStates = new(DiscordComparers.UserComparer);
        Board board = default!;

        public DiscordMoniesGameInstance(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators) 
            : base(id, client, players, spectators)
        {
            originalPlayerCount = players.Length;
        }


        public override async Task Initialize()
        { 
            var asm = GetType().Assembly;

            using var jsonStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.board.json");
            board = await Board.BoardFromJson(jsonStream!);

            var embed = new EmbedBuilder()
            {
                Title = "Balance",
                Description = $"The game has started! Every player has been given Ð{board.StartingMoney:N0}",
                Color = Color.Green
            }.Build();
            await this.Broadcast("", embed: embed);

        }
        
        public override async Task OnMessage(IUserMessage msg, int pos)
        {
            var msgContent = msg.Content[pos..];
            if (msgContent == "drop")
            {
                DropPlayer(msg.Author);
                return;
            }

            if (!Spectators.Contains(msg.Author, DiscordComparers.UserComparer))
            {
                //player-only commands
                if (msgContent == "bal")
                {
                    var embed = new EmbedBuilder()
                    {
                        Title = "Balance",
                        Description = $"Your balance is **Ð{userStates[msg.Author].Money:N0}.**",
                        Color = Color.Gold
                    }.Build();
                    await this.BroadcastTo("", embed: embed, players: msg.Author);
                    return;
                }
            }

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