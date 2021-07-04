using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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


        public BoardRenderer()
        {
            var asm = GetType().Assembly;
            using var boardStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.board.png")!;
            using var pieceStream = asm.GetManifestResourceStream("DiscordMoniesGame.Resources.piece.png")!;
            baseBoard = new Bitmap(boardStream);
            basePiece = new Bitmap(pieceStream);
        }

        public Bitmap Render(ImmutableArray<IUser> players, 
            ConcurrentDictionary<IUser, DiscordMoniesGameInstance.UserState> playerStates, Board board)
        {
            var timer = new Stopwatch();
            timer.Start();
            var bmp = new Bitmap(baseBoard);
            using var gfx = Graphics.FromImage(bmp);
            
            foreach (var p in players)
            {
                var bounds = playerStates[p].Jailed
                    ? board.JailBounds
                    : board.BoardSpaces[playerStates[p].Position].Bounds;
                var hash = p.Id.GetHashCode() ^ bounds.GetHashCode();
                var xOff = (hash & 0xFF) / 255.0;
                var yOff = ((hash & 0xFF00) >> 8) / 255.0;
                var xPos = (int)(xOff * (bounds.Width - basePiece.Width) + (bounds.X + basePiece.Width / 2));
                var yPos = (int)(yOff * (bounds.Height - basePiece.Height) + (bounds.Y + basePiece.Height / 2));
                using var colored = ColoredPiece(playerStates[p].Color);

                gfx.DrawImage(colored, xPos - basePiece.Height / 2, yPos - basePiece.Width / 2);
            }
            timer.Stop();
            Console.WriteLine($"{timer.ElapsedMilliseconds}ms to render");
            return bmp;
        }

        Bitmap ColoredPiece(Color color)
        {
            var bmp = new Bitmap(basePiece);
            var data = bmp.LockBits(new(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var c = color.ToArgb();
            unsafe
            {
                var s = new Span<int>(data.Scan0.ToPointer(), data.Width * data.Height);
                for (var i = 0; i < s.Length; i++)
                {
                    s[i] &= c;
                }
            }
            bmp.UnlockBits(data);
            return bmp;
        }

        public void Dispose()
        { 
            baseBoard.Dispose();
            basePiece.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
