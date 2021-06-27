using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace Leisure
{
    /// <summary>
    /// Represents an instance of a game.
    /// </summary>
    public abstract class GameInstance : IEquatable<GameInstance>
    {
        /// <summary>
        /// Creates a new instance of a game.
        /// </summary>
        /// <param name="client">The client which the new game will use.</param>
        /// <param name="id">The ID of the new game.</param>
        /// <param name="players">The players that are in the game.</param>
        /// <param name="spectators">The spectators that are in the game.</param>
        protected GameInstance(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators = default)
        {
            Client = client;
            Id = id;
            Players = players;
            Spectators = spectators;
        }

        /// <summary>
        /// Gets the players playing the game.
        /// </summary>
        public ImmutableArray<IUser> Players { get; private set; }
        
        /// <summary>
        /// Gets the spectators spectating the game.
        /// </summary>
        public ImmutableArray<IUser> Spectators { get; private set; }

        /// <summary>
        /// Gets all of the players and spectators in the game.
        /// </summary>
        public IEnumerable<IUser> Users => Players.Union(Spectators, DiscordComparers.UserComparer);
        
        /// <summary>
        /// Get the ID of the game.
        /// </summary>
        public int Id { get; }
        
        /// <summary>
        /// Gets the client that is running the game.
        /// </summary>
        public IDiscordClient Client { get; }

        /// <summary>
        /// This is ran when the game has been closed, and is ready to be initialized.
        /// </summary>
        public abstract Task Initialize();

        /// <summary>
        /// Ran when Leisure.NET detects a message for your game.
        /// </summary>
        /// <param name="msg">The message sent</param>
        /// <param name="pos">The position at which commands to the game start</param>
        public abstract Task OnMessage(IUserMessage msg, int pos);

        /// <summary>
        /// Invoked when the game has signaled that it is about to close.
        /// </summary>
        public event EventHandler? Closing;
        
        /// <summary>
        /// Invoked when the a user has requested to drop from the game.
        /// </summary>
        public event EventHandler<UserDroppingEventArgs>? UserDropping;

        /// <summary>
        /// Invokes the <see cref="Closing"/> event from derived classes.
        /// </summary>
        protected void OnClosing()
        {
            Closing?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Invokes the <see cref="UserDropping"/> event from derived classes.
        /// </summary>
        protected void OnDroppingUser(IUser user)
        {
            if (Players.Contains(user, DiscordComparers.UserComparer))
                Players = Players.Remove(user, DiscordComparers.UserComparer);
            else if (Spectators.Contains(user, DiscordComparers.UserComparer))
                Spectators = Spectators.Remove(user, DiscordComparers.UserComparer);
            else
                throw new ArgumentException("The given user is not playing in or spectating this game.", nameof(user));

            UserDropping?.Invoke(this, new UserDroppingEventArgs(user));
        }

        /// <inheritdoc />
        public bool Equals(GameInstance? other) => Id == other?.Id;

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((GameInstance) obj);
        }

        /// <summary>
        /// Compares two game instances for equality.
        /// </summary>
        public static bool operator ==(GameInstance? left, GameInstance? right) => left?.Equals(right) ?? false;

        /// <summary>
        /// Compares two game instances for inequality.
        /// </summary>
        public static Boolean operator !=(GameInstance? left, GameInstance? right) => !(left == right);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Id, Client);
    }
}