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
        readonly ConcurrentDictionary<IUser, UserState> userStates = new();
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
            await this.Broadcast($"The game has started! Every player has been given Ð{board.StartingMoney:N0}");

        }
        
        public override async Task OnMessage(IUserMessage msg, int pos)
        {
            if (msg.Content.AsSpan(pos).Equals("drop", default))
            {
                DropPlayer(msg.Author);
                return;
            }

            // Do not react to spectator messages
            if (Spectators.Contains(msg.Author, DiscordComparers.UserComparer))
                return;
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