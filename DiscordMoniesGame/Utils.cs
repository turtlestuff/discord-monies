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

        public static IUser MatchClosest(string name, IEnumerable<IUser> users) =>
            users.Select(u => (Dist: Distance(name.ToLowerInvariant(), u.Username.ToLowerInvariant()), User: u)).OrderBy(i => i.Dist).First().User;

        public static string BuildingsAsString(this int houses) => houses switch
        {
            0 => "None",
            1 => "1 house",
            var x when x > 1 && x < 5 => $"{x} houses",
            5 => "Hotel",
            _ => "Invalid"
        };

        public static string MoneyString(this int money) => $"`Ð{money:N0}`";

        public static string LocString(this int position)
        {
            var letter = (char)('A' + (int) Math.Floor(position / 10.0));
            var number = (position % 10).ToString();
            return $"{letter}{number}";
        }
    }
}
