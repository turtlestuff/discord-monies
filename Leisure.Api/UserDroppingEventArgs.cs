using Discord;

namespace Leisure
{
    /// <summary>
    /// Provides data for the <see cref="GameInstance.UserDropping" /> event. />
    /// </summary>
    public readonly struct UserDroppingEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="droppingUser">The user that is dropping from the game.</param>
        public UserDroppingEventArgs(IUser droppingUser)
        {
            DroppingUser = droppingUser;
        }
            
        /// <summary>
        /// Gets the user that is dropping from the game.
        /// </summary>
        public IUser DroppingUser { get; }
    }
    
}