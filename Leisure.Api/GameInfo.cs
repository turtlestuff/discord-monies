using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Discord;

namespace Leisure
{
    /// <summary>
    /// Represents the information about a game.
    /// </summary>
    public abstract class GameInfo
    {
        /// <summary>
        /// The full name of the game.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// A description of the game.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// The version of the game. 
        /// </summary>
        public virtual string Version => GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "null version";

        /// <summary>
        /// The author(s) of the games.
        /// </summary>
        public abstract string Author { get; }

        /// <summary>
        /// A short description of the amount of players a game can accept.
        /// </summary>
        public abstract string PlayerCountDescription { get; }

        /// <summary>
        /// The prefix used by the game. The prefix is used to get information about the game and open new lobbies for the game.
        /// </summary>
        public abstract string Prefix { get; }

        /// <summary>
        /// Gets an icon to display for the game. <see langword="null" /> represents an empty icon.
        /// </summary>
        public abstract string? IconUrl { get; }

        /// <summary>
        /// Checks whether a certain number of players is valid for a game.
        /// </summary>
        /// <param name="playerCount">Amount of players to check.</param>
        /// <returns>True if the amount is valid.</returns>
        public virtual bool IsValidPlayerCount(int playerCount) => true;
        
        /// <summary>
        /// Gets whether the game supports users joining as spectators.
        /// </summary>
        public abstract bool SupportsSpectators { get; }

        /// <summary>
        /// Creates the game.
        /// </summary>
        /// <param name="client">The client the new game will use.</param>
        /// <param name="players">The players in the new game.</param>
        /// <param name="spectators">The spectators in the new game.</param>
        /// <param name="id">The ID of the game.</param>
        /// <returns>The new <see cref="Leisure.GameInstance"/></returns>
        public abstract GameInstance CreateGame(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators);
    }
}