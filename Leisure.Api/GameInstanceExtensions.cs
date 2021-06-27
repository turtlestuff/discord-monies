using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace Leisure
{
    /// <summary>
    /// Provides methods to broadcast messages to members of games.
    /// </summary>
    public static class GameInstanceExtensions
    {
        /// <summary>
        /// Broadcasts a message to every user in the game.
        /// </summary>
        /// <param name="game">The game to broadcast the message to.</param>
        /// <param name="text">Text to broadcast</param>
        /// <param name="isTTS">Message send as TTS? (default is false)</param>
        /// <param name="embed">Embed to send (default is none)</param>
        /// <returns></returns>
        public static async Task Broadcast(this GameInstance game, string text, bool isTTS = false, Embed? embed = default)
        {
            foreach (var player in game.Users)
            {
                await player.SendMessageAsync(text, isTTS, embed);
            }
        }

        /// <summary>
        /// Broadcasts a message to every user in <paramref name="players"/>.
        /// </summary>
        /// <param name="game">The game to broadcast the message to. </param>
        /// <param name="text">The message to broadcast.</param>
        /// <param name="isTTS">If true, the message will be read using text to speech.</param>
        /// <param name="embed">The embed to send the message with.</param>
        /// <param name="players">The users to send the message to.</param>
        public static async Task BroadcastTo(this GameInstance game, string text, bool isTTS = false, Embed? embed = default, params IUser[] players)
        {
            foreach (var player in players)
            {
                await player.SendMessageAsync(text, isTTS, embed);
            }
        }

        /// <summary>
        /// Broadcasts a message to every user in <paramref name="players"/>.
        /// </summary>
        /// <param name="game">The game to broadcast the message to. </param>
        /// <param name="text">The message to broadcast.</param>
        /// <param name="isTTS">If true, the message will be read using text to speech.</param>
        /// <param name="embed">The embed to send the message with.</param>
        /// <param name="players">The users to send the message to.</param>
        /// <typeparam name="TEnumerable">The type of enumerable.</typeparam>
        public static async Task BroadcastTo<TEnumerable>(this GameInstance game, string text, bool isTTS = false, Embed? embed = default, TEnumerable players = default)
            where TEnumerable : struct, IEnumerable<IUser>
        {
            foreach (var player in players)
            {
                await player.SendMessageAsync(text, isTTS, embed);
            }
        }
        
        /// <summary>
        /// Broadcasts a message to every user not in <paramref name="exclude"/>.
        /// </summary>
        /// <param name="game">The game to broadcast the message to. </param>
        /// <param name="text">Text to broadcast</param>
        /// <param name="isTTS">Message send as TTS? (default is false)</param>
        /// <param name="embed">Embed to send (default is none)</param>
        /// <param name="exclude">Users to exclude.</param>
        public static async Task BroadcastExcluding(this GameInstance game, string text, bool isTTS = false, Embed? embed = default, params IUser[] exclude)
        {
            foreach (var p in game.Users.Except(exclude, DiscordComparers.UserComparer))
            {
                await p.SendMessageAsync(text, isTTS, embed);
            }
        }
    }
}