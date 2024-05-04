using System.Collections.Immutable;
using DiscordMonies.Board;

using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

using System.Drawing;
using DiscordMonies.Game.Trade;

namespace DiscordMonies.Game;

public record NamedColor(string Name, Color Color);

public sealed record GamePlayer : IUser
{
    public static readonly ImmutableArray<NamedColor> ColorList = [
        new("red", System.Drawing.Color.FromArgb(255, 70, 70)),
        new("orange", System.Drawing.Color.FromArgb(255, 163, 76)),
        new("yellow", System.Drawing.Color.FromArgb(255, 251, 140)),
        new("green", System.Drawing.Color.FromArgb(118, 202, 38)),
        new("cyan", System.Drawing.Color.FromArgb(31, 255, 255)),
        new("blue", System.Drawing.Color.FromArgb(140, 143, 232)),
        new("pink", System.Drawing.Color.FromArgb(255, 136, 210)),
        new("gray", System.Drawing.Color.FromArgb(192, 192, 192))
    ];
    
    public required IGuildMember GuildMember { get; set; }
    public required IUser User { get; set; }
    public required Snowflake Id { get; init; }

    public required string DisplayName { get; set; }

    public required GameState GameState { get; init; }

    public int JailTurns { get; set; } = -1;

    public int Position { get; set; }
    
    public required NamedColor Color { get; init; }
    
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
    
    public TradeTable? ConfiguringTradeTable { get; set; }

    #endregion
}
