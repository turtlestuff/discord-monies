using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Color = System.Drawing.Color;

namespace DiscordMoniesGame
{
    public class BoardRenderer : IDisposable
    {
        public static readonly Color[] Colors = new[]
        {
            Color.FromArgb(255, 146, 146), //Red
            Color.FromArgb(255, 204, 109), //Orange
            Color.FromArgb(255, 251, 140), //Yellow
            Color.FromArgb(181, 255, 109), //Green
            Color.FromArgb(152, 244, 255), //Cyan
            Color.FromArgb(161, 164, 236)  //Blue
        };

        readonly Bitmap baseBoard;
        readonly Bitmap basePiece;
        readonly Bitmap owned;
        readonly Bitmap mortgaged;
        readonly Bitmap house1;
        readonly Bitmap house2;
        readonly Bitmap house3;
        readonly Bitmap house4;
        readonly Bitmap hotel;

        public BoardRenderer()
        {
            var asm = GetType().Assembly;
            using var boardStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.board.png")!;
            using var pieceStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.piece.png")!;
            using var ownedStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.owned.png")!;
            using var mortgagedStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.mortgaged.png")!;
            using var house1Stream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.1house.png")!;
            using var house2Stream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.2houses.png")!;
            using var house3Stream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.3houses.png")!;
            using var house4Stream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.4houses.png")!;
            using var hotelStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.hotel.png")!;

            baseBoard = new(boardStream);
            basePiece = new(pieceStream);
            owned = new(ownedStream);
            mortgaged = new(mortgagedStream);
            house1 = new(house1Stream);
            house2 = new(house2Stream);
            house3 = new(house3Stream);
            house4 = new(house4Stream);
            hotel = new(hotelStream);
        }

        public Bitmap Render(ImmutableArray<IUser> players, 
            ConcurrentDictionary<IUser, DiscordMoniesGameInstance.PlayerState> playerStates, Board board)
        {
            var bmp = new Bitmap(baseBoard); // dont need to using because we are returning this
            using var gfx = Graphics.FromImage(bmp);
            
            foreach (var p in players)
            {
                var bounds = playerStates[p].JailStatus != -1
                    ? board.JailBounds
                    : board.BoardSpaces[playerStates[p].Position].Bounds;

                var array = new BigInteger(p.Id).ToByteArray().Union(new BigInteger(bounds.GetHashCode()).ToByteArray()).ToArray();
                var hash = MD5.Create().ComputeHash(array); 
                var xOff = hash[0] / 255.0;
                var yOff = hash[1] / 255.0;
                var xPos = (int)(xOff * (bounds.Width - basePiece.Width) + (bounds.X + basePiece.Width / 2));
                var yPos = (int)(yOff * (bounds.Height - basePiece.Height) + (bounds.Y + basePiece.Height / 2));
                using var c = Colored(basePiece, playerStates[p].Color);

                gfx.DrawImage(c, xPos - basePiece.Height / 2, yPos - basePiece.Width / 2);
            }

            foreach (var s in board.BoardSpaces)
            {
                if (s is not PropertySpace ps || ps.Owner is null)
                    continue;

                var pos = new Point(s.Bounds.X + 5, s.Bounds.Y + 5);
                var playerColor = playerStates[ps.Owner].Color;
                if (ps.Mortgaged)
                {
                    using var c = Colored(mortgaged, playerColor);
                    gfx.DrawImage(c, pos);
                    continue;
                }

                if (ps is RoadSpace rs)
                {
                    var piece = rs.Houses switch
                    {
                        1 => house1,
                        2 => house2,
                        3 => house3,
                        4 => house4,
                        5 => hotel,
                        _ => owned,
                    };
                    using var c = Colored(piece, playerColor);
                    gfx.DrawImage(c, pos);
                    continue;
                }

                using var colored = Colored(owned, playerColor);
                gfx.DrawImage(colored, pos);
            }

            return bmp;
        }

        static Bitmap Colored(Bitmap bitmap, Color color)
        {
            var bmp = new Bitmap(bitmap);
            var data = bmp.LockBits(new(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            unsafe
            {
                var s = new Span<int>(data.Scan0.ToPointer(), data.Width * data.Height);
                for (var i = 0; i < s.Length; i++)
                {
                    var bc = Color.FromArgb(s[i]);
                    s[i] = Color.FromArgb(bc.A,
                        bc.R * color.R / 255, 
                        bc.G * color.G / 255, 
                        bc.B * color.B / 255).ToArgb();
                }
            }
            bmp.UnlockBits(data);
            return bmp;
        }

        public void Dispose()
        { 
            baseBoard.Dispose();
            basePiece.Dispose();
            owned.Dispose();
            mortgaged.Dispose();
            house1.Dispose();
            house2.Dispose();
            house3.Dispose();
            house4.Dispose();
            hotel.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
