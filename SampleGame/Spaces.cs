using Discord;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DiscordMoniesGame
{
    public record Space(string Name, SpaceBounds Bounds);
    public record DrawCardSpace(string Name, CardType Type, SpaceBounds Bounds) : Space(Name, Bounds);
    public record GoSpace(string Name, int Value, SpaceBounds Bounds) : Space(Name, Bounds);
    public record TaxSpace(string Name, int Value, SpaceBounds Bounds) : Space(Name, Bounds);
    public record PropertySpace(string Name, int Value, IUser Owner, SpaceBounds Bounds) : Space(Name, Bounds);

    public record struct SpaceBounds(int X, int Y, int Width, int Height);

    public enum CardType
    { 
        Chance,
        Chest
    }
}
