using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using Color = System.Drawing.Color;

namespace DiscordMoniesGame
{
    public class BoardRenderer : IDisposable
    {
        public static readonly Color[] Colors = new[]
        {
            Color.Red,
            Color.Orange,
            Color.Yellow,
            Color.Green,
            Color.Cyan,
            Color.Blue
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
            var bmp = new Bitmap(baseBoard);
            using var gfx = Graphics.FromImage(bmp);
            
            foreach (var p in players)
            {
                var bounds = playerStates[p].Jailed
                    ? board.JailBounds
                    : board.BoardSpaces[playerStates[p].Position].Bounds;
                var hash = p.Id.GetHashCode();
                var xOff = (hash & 0xFF) / 255.0;
                var yOff = ((hash & 0xFF00) >> 8) / 255.0;
                var xPos = (int)xOff * bounds.Width + bounds.X;
                var yPos = (int)yOff * bounds.Height + bounds.Y;

                gfx.DrawImage(basePiece, xPos - basePiece.Height / 2, yPos - basePiece.Width / 2);
            }

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
