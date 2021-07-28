using Discord;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordMoniesGame
{
    public static class Utils
    {
        public static int Distance(string a, string b) =>
             Enumerable.Range(0, Math.Min(a.Length, b.Length)).Select((_, i) => a[i] != b[i] ? 1 : 0).Sum();

        public static T? MatchClosest<T>(string name, IEnumerable<T> users, Func<T, string> selector)
        {
            var (dist, val) = users.Select(x => (Dist: Distance(name.ToLowerInvariant(), selector(x).ToLowerInvariant()), Val: x)).OrderBy(i => i.Dist).First();
            if (dist / ((double)Math.Min(name.Length, selector(val).Length)) < 1.0 / 3.0)
            {
                return val;
            }
            else
            {
                return default;
            }
        }

        public static string? MatchClosest(string input, IEnumerable<string> strings) => MatchClosest(input, strings, x => x);

        public static string BuildingsAsString(this int houses) => houses switch
        {
            0 => "None",
            1 => "1 house",
            var x when x > 1 && x < 5 => $"{x} houses",
            5 => "Hotel",
            _ => "Invalid"
        };

        public static string MoneyString(this int money) => $"`{money:Ð#,0}`";

        public static string LocString(this int position)
        {
            var letter = (char)('A' + (int)Math.Floor(position / 10.0));
            var number = (position % 10).ToString();
            return $"{letter}{number}";
        }

        public static string CommaAndList(this string[] els) => els.Length > 1 ? string.Join(", ", els[..^1]) + " and " + els[^1] : els[0];

        public static EmbedBuilder WithId(this EmbedBuilder e, int id)
        {
            if (e.Footer is null)
            {
                e.Footer = new();
            }
            e.Footer.Text += $" Game #{id}";
            return e;
        }
    }
}
