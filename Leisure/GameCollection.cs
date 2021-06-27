using System.Collections.Concurrent;

namespace Leisure
{
    /// <summary>
    /// Information about the game the user is in and their current game.
    /// </summary>
    public class GameCollection
    {
        /// <summary>
        /// The games which the user is in.
        /// </summary>
        public ConcurrentDictionary<int, GameInstance> Games { get; } = new ConcurrentDictionary<int, GameInstance>();

        /// <summary>
        /// The game the user is currently interacting with.
        /// </summary>
        public GameInstance CurrentGame { get; set; } = default!;

        /// <inheritdoc />
        public override string ToString()
        {
            return $"({CurrentGame.Id})";
        }
    }
}