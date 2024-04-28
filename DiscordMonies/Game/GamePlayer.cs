using DiscordMonies.Board;

using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

using System.Drawing;

namespace DiscordMonies.Game;

public sealed record GamePlayer : IUser
{
    public required IGuildMember GuildMember { get; set; }
    public required IUser User { get; set; }
    public required Snowflake Id { get; set; }

    public required string DisplayName { get; set; }

    public required GameState GameState { get; set; }


    public int Position { get; set; } = 0;
    public Space CurrentSpace => GameState.Board.Spaces[Position];

    public required int Money { get; set; }
    public string MoneyString() => Money.MoneyString();
   
    public bool Equals(GamePlayer? other)
    {
        return Id == other?.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return DisplayName;
    }

    #region IUser Implementation

    Snowflake IUser.ID => User.ID;

    string IUser.Username => User.Username;

    ushort IUser.Discriminator => User.Discriminator;

    Optional<string?> IUser.GlobalName => User.GlobalName;

    IImageHash? IUser.Avatar => User.Avatar;

    Optional<bool> IUser.IsBot => User.IsBot;

    Optional<bool> IUser.IsSystem => User.IsSystem;

    Optional<bool> IUser.IsMFAEnabled => User.IsMFAEnabled;

    Optional<IImageHash?> IUser.Banner => User.Banner;

    Optional<Color?> IUser.AccentColour => User.AccentColour;

    Optional<string> IUser.Locale => User.Locale;

    Optional<bool> IUser.IsVerified => User.IsVerified;

    Optional<string?> IUser.Email => User.Email;

    Optional<UserFlags> IUser.Flags => User.Flags;

    Optional<PremiumType> IUser.PremiumType => User.PremiumType;

    Optional<UserFlags> IUser.PublicFlags => User.PublicFlags;

    Optional<IImageHash?> IUser.AvatarDecoration => User.AvatarDecoration;

    #endregion
}
