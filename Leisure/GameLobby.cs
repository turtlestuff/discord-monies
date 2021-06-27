using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace Leisure
{
    /// <summary>
    /// Represents a game that is about to start.
    /// </summary>
    public class GameLobby
    {
        internal GameLobby(int id, IUser startingUser, IMessageChannel channel, GameInfo gameInfo)
        {
            Id = id;
            StartingUser = startingUser;
            Players.Add(startingUser);
            Channel = channel;
            GameInfo = gameInfo;
        }

        /// <summary>
        /// The members in the lobby that will join the game when it starts.
        /// </summary>
        public ImmutableArray<IUser>.Builder Players { get; } = ImmutableArray.CreateBuilder<IUser>();
        
        /// <summary>
        /// The members in the lobby that will spectate the game when it starts.
        /// </summary>
        public ImmutableArray<IUser>.Builder Spectators { get; } = ImmutableArray.CreateBuilder<IUser>();

        /// <summary>
        /// All the members in the lobby.
        /// </summary>
        public IEnumerable<IUser> Users => Players.Union(Spectators, DiscordComparers.UserComparer);

        /// <summary>
        /// The ID of the game that will start.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// The user that opened the lobby.
        /// </summary>
        public IUser StartingUser { get; }

        /// <summary>
        /// The channel in which the lobby was opened.
        /// </summary>
        public IMessageChannel Channel { get; }

        /// <summary>
        /// The game that will be played when the lobby closes.
        /// </summary>
        public GameInfo GameInfo { get; }
    }
}