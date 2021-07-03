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
            users.Select(u => (Dist: Distance(name, u.Username), User: u)).OrderBy(i => i.Dist).First().User;
    }
}
