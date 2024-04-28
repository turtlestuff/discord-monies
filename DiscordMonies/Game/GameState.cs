using DiscordMonies.Board;

using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

using System.Drawing;
using Stateless;

namespace DiscordMonies.Game;

public partial class GameState
{
    public required int GameId { get; init; }

    public required Snowflake ThreadId { get; init; }

    public required Snowflake GuildId { get; init; }

    public required List<Snowflake> PlayerIds { get; init; }

    public required List<GamePlayer> Players { get; init; }

    public int CurrentPlayerIndex { get; set; } = 0;
    public GamePlayer CurrentPlayer => Players[CurrentPlayerIndex];

    public GamePlayer? TryGetById(Snowflake userId) 
        => Players.FirstOrDefault(p => p.Id == userId);

    public GamePlayer? TryGetById(IUser user) => TryGetById(user.ID);

    public GamePlayer GetById(Snowflake userId)
        => Players.First(p => p.Id == userId);

    public GamePlayer GetById(IUser user) => GetById(user.ID);
    
    public required GameBoard Board { get; init; }

    public GameDie Die { get; } = new();

    public enum StateMachineState
    {
        Idle,
        ForAuctionOrBuyDecision,
        ForAuctionToFinish,
        ForOtherJailDecision,
        ForArrearsPay,
        ForTakeCardOrPayDecision
    }

    public StateMachineState State { get; set; }
}

public sealed record GamePlayer(IGuildMember GuildMember, IUser User, string DisplayName, Snowflake Id, PlayerState State) : IUser
{
    public IGuildMember GuildMember { get; set; } = GuildMember;

    public IUser User { get; set; } = User;

    public string DisplayName { get; set; } = DisplayName;

    public Snowflake Id { get; } = Id;

    public bool Equals(GamePlayer? other)
    {
        return Id == other?.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public Snowflake ID => User.ID;

    public string Username => User.Username;

    public ushort Discriminator => User.Discriminator;

    public Optional<string?> GlobalName => User.GlobalName;

    public IImageHash? Avatar => User.Avatar;

    public Optional<bool> IsBot => User.IsBot;

    public Optional<bool> IsSystem => User.IsSystem;

    public Optional<bool> IsMFAEnabled => User.IsMFAEnabled;

    public Optional<IImageHash?> Banner => User.Banner;

    public Optional<Color?> AccentColour => User.AccentColour;

    public Optional<string> Locale => User.Locale;

    public Optional<bool> IsVerified => User.IsVerified;

    public Optional<string?> Email => User.Email;

    public Optional<UserFlags> Flags => User.Flags;

    public Optional<PremiumType> PremiumType => User.PremiumType;

    public Optional<UserFlags> PublicFlags => User.PublicFlags;

    public Optional<IImageHash?> AvatarDecoration => User.AvatarDecoration;
}
