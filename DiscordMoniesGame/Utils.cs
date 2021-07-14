using Discord;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordMoniesGame
{
    public static class Utils
    {
        public static int Distance(string a, string b)
        {
            var mat = new int[a.Length + 1, b.Length + 1];

            for (var i = 0; i < a.Length; i++)
                mat[i + 1, 0] = i + 1;
            
            for (var i = 0; i < b.Length; i++)
                mat[0, i + 1] = i + 1;
            
            for (var i = 0; i < a.Length; i++)
            {
                for (var j = 0; j < b.Length; j++)
                {
                    var substitutionCost = a[i] == b[j] ? 0 : 1;

                    mat[i + 1, j + 1] = Math.Min(
                        Math.Min(mat[i, j + 1] + 1, mat[i + 1, j] + 1),
                        mat[i, j] + substitutionCost);
                }
            }
            return mat[a.Length, b.Length];
        }

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

        public static string PositionString(this int position)
        {
            var letter = (char)('A' + (int) Math.Floor(position / 10.0));
            var number = (position % 10).ToString();
            return $"{letter}{number}";
        }
    }
}
