using System.Collections.Immutable;
using Discord;
using Leisure;

namespace DiscordMoniesGame
{
    public sealed class DiscordMoniesGameInfo : GameInfo
    {
        public override string Name => "Discord Monies";
        public override string Description => "μονοπώλῐον";
        public override string Author => "Vrbt, refletstronits, and Swuif HGrull";
        public override string Prefix => "dm";
        public override string IconUrl => "https://cdn.discordapp.com/avatars/614127017666936835/3290f301318a46c34e8a4d0671abeff4.png?size=2048";
        public override string PlayerCountDescription => "2\u20116"; // non breaking hyphen
        public override GameInstance CreateGame(int id, IDiscordClient client, ImmutableArray<IUser> players, ImmutableArray<IUser> spectators) => new DiscordMoniesGameInstance(id, client, players, spectators);
        public override bool IsValidPlayerCount(int i) => (i >= 1) && (i <= 6);
        public override bool SupportsSpectators => true;
    }
}