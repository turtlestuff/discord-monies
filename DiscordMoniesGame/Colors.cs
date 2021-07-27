using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DiscordMoniesGame
{
    public static class Colors
    {
        public static readonly Dictionary<string, Color> ColorList = new()
        {
            { "red", Color.Red },
            { "orange", Color.Orange },
            { "yellow", Color.Yellow },
            { "green", Color.Green },
            { "cyan", Color.Cyan },
            { "blue", Color.Blue },
            { "pink", Color.Pink }, 
            { "gray", Color.Gray }
        };

        public static Color ColorOfName(string name) => ColorList[name];
        public static string NameOfColor(Color color) => ColorList.First(x => x.Value == color).Key;

        public static Discord.Color ToDiscordColor(this Color color) => new(color.R, color.G, color.B);
    }
}