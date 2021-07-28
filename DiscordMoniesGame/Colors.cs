using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DiscordMoniesGame
{
    public static class Colors
    {
        public static readonly Dictionary<string, Color> ColorList = new()
        {
            { "red", Color.FromArgb(255, 70, 70) },
            { "orange", Color.FromArgb(255, 204, 109) },
            { "yellow", Color.FromArgb(255, 251, 140) },
            { "green", Color.FromArgb(128, 255, 94) },
            { "cyan", Color.FromArgb(31, 255, 255) },
            { "blue", Color.FromArgb(140, 143, 232) },
            { "pink", Color.FromArgb(233, 143, 224) }, 
            { "gray", Color.FromArgb(192, 192, 192) }
        };

        public static Color ColorOfName(string name) => ColorList[name];
        public static string NameOfColor(Color color) => ColorList.First(x => x.Value == color).Key;

        public static Discord.Color ToDiscordColor(this Color color) => new(color.R, color.G, color.B);
    }
}