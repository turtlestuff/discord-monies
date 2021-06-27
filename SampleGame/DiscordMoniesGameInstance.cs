using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Leisure;

namespace MathOlympics
{
    public sealed class DiscordMoniesGameInstance : GameInstance
    {       
        record UserState (int Money = 1500);

        readonly int originalPlayerCount;
        readonly ConcurrentDictionary<IUser, UserState> userStates;

        public DiscordMoniesGameInstance(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators) 
            : base(id, client, players, spectators)
        {
            originalPlayerCount = players.Length;
            userStates = new(players.Select(p => new KeyValuePair<IUser, UserState>(p, new UserState())));
        }


        public override async Task Initialize()
        {   
            await this.Broadcast("The game has started! Every player has been given Ð1.500")
        }
        
        public override async Task OnMessage(IUserMessage msg, int pos)
        {
            if (msg.Content.AsSpan(pos).Equals("drop", default))
            {
                DropPlayer(msg.Author);
                return;
            }

            Close();
            // Do not react to spectator messages
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