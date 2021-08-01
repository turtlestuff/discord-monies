using Discord;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
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
        readonly SKBitmap baseBoard;
        readonly SKBitmap basePiece;
        readonly SKBitmap owned;
        readonly SKBitmap mortgaged;
        readonly SKBitmap house1;
        readonly SKBitmap house2;
        readonly SKBitmap house3;
        readonly SKBitmap house4;
        readonly SKBitmap hotel;

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
           
            baseBoard = SKBitmap.Decode(boardStream);
            basePiece = SKBitmap.Decode(pieceStream);
            owned = SKBitmap.Decode(ownedStream);
            mortgaged = SKBitmap.Decode(mortgagedStream);
            house1 = SKBitmap.Decode(house1Stream);
            house2 = SKBitmap.Decode(house2Stream);
            house3 = SKBitmap.Decode(house3Stream);
            house4 = SKBitmap.Decode(house4Stream);
            hotel = SKBitmap.Decode(hotelStream);
        }

        public SKBitmap Render(ImmutableArray<IUser> players,
            ConcurrentDictionary<IUser, DiscordMoniesGameInstance.PlayerState> playerStates, Board board)
        {
            var bmp = baseBoard.Copy(); // dont need to using because we are returning this
            var canvas = new SKCanvas(bmp);

            foreach (var space in board.Spaces)
            {
                if (space is not PropertySpace ps || ps.Owner is null)
                {
                    continue;
                }
                
                var pos = new SKPoint(space.Bounds.X + 5, space.Bounds.Y + 5);
                var color = playerStates[ps.Owner].Color;
                
                if (ps.Mortgaged)
                {
                    using var c = Colored(mortgaged, color);
                    canvas.DrawBitmap(c, pos);
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
                    using var c = Colored(piece, color);
                    canvas.DrawBitmap(c, pos);
                    continue;
                }

                using var col = Colored(owned, color);
                canvas.DrawBitmap(col, pos);
            }

            foreach (var player in players)
            {
                var bounds = playerStates[player].JailStatus != -1 ?
                    board.JailBounds :
                    board.Spaces[playerStates[player].Position].Bounds;

                var hash = HashCode.Combine(player.Id, bounds);
                var xOff = (hash & 0xFF) / 255.0;
                var yOff = ((hash >> 8) & 0xFF) / 255.0;
                var xPos = (int)(xOff * (bounds.Width - basePiece.Width) + (bounds.X + basePiece.Width / 2));
                var yPos = (int)(yOff * (bounds.Height - basePiece.Height) + (bounds.Y + basePiece.Height / 2));
                using var c = Colored(basePiece, playerStates[player].Color);
                canvas.DrawBitmap(c, new SKPoint(xPos - basePiece.Height / 2, yPos - basePiece.Height / 2));
            }

            return bmp;
        }

        
        static SKBitmap Colored(SKBitmap bitmap, Color color)
        {
            var bmp = bitmap.Copy();
            var cnv = new SKCanvas(bmp);
            cnv.DrawColor(new(color.R, color.G, color.B), SKBlendMode.Modulate);
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
