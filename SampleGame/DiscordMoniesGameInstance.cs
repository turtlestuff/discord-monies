using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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

        public DiscordMoniesGameInstance(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators) 
            : base(id, client, players, spectators)
        {
            originalPlayerCount = players.Length;
        }


        public override async Task Initialize()
        {
            await this.Broadcast("The game has started! Every player has been given Ð1.500");

            var asm = GetType().Assembly;

            using var stream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.board.png");
            foreach (var p in Players)
                await p.SendFileAsync(stream!, "board.png");
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