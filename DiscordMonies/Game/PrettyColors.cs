using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Drawing;

namespace DiscordMonies.Game;

public static class PrettyColors
{
    public static readonly Color Red = Color.FromArgb(0xE74C3C);

    public static readonly Color Green = Color.FromArgb(0x2ECC71);

    public static readonly Color Gold = Color.FromArgb(0xF1C40F);

    public static readonly ImmutableArray<NamedColor> ColorList =
    [
        new("red", Color.FromArgb(255, 70, 70)),
        new("orange", Color.FromArgb(255, 163, 76)),
        new("yellow", Color.FromArgb(255, 251, 140)),
        new("green", Color.FromArgb(118, 202, 38)),
        new("cyan", Color.FromArgb(31, 255, 255)),
        new("blue", Color.FromArgb(140, 143, 232)),
        new("pink", Color.FromArgb(255, 136, 210)),
        new("gray", Color.FromArgb(192, 192, 192))
    ];

    public static readonly FrozenDictionary<string, Color> ColorMap = ColorList.ToFrozenDictionary(k => k.Name, k => k.Color);
}

public sealed record NamedColor(string Name, Color Color);
