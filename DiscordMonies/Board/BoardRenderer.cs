using System.Drawing;
using DiscordMonies.Game;
using Remora.Discord.API.Abstractions.Rest;
using SkiaSharp;

// ReSharper disable PossibleLossOfFraction

namespace DiscordMonies.Board;

public sealed record HighlightedSpace(Space Space, Color Color);

public class BoardRenderer : IDisposable
{
    private readonly GameState _state;
    private readonly SKBitmap _baseBoard;
    private readonly SKBitmap _basePiece;
    private readonly SKBitmap _owned;
    private readonly SKBitmap _mortgaged;
    private readonly SKBitmap _house1;
    private readonly SKBitmap _house2;
    private readonly SKBitmap _house3;
    private readonly SKBitmap _house4;
    private readonly SKBitmap _hotel;
    private readonly SKBitmap _arrow;

    public BoardRenderer(GameState state)
    {
        _state = state;
        var asm = GetType().Assembly;
        using var boardStream = asm.GetManifestResourceStream("DiscordMonies.Resources.board.png")!;
        using var pieceStream = asm.GetManifestResourceStream("DiscordMonies.Resources.piece.png")!;
        using var ownedStream = asm.GetManifestResourceStream("DiscordMonies.Resources.owned.png")!;
        using var mortgagedStream = asm.GetManifestResourceStream("DiscordMonies.Resources.mortgaged.png")!;
        using var house1Stream = asm.GetManifestResourceStream("DiscordMonies.Resources.1house.png")!;
        using var house2Stream = asm.GetManifestResourceStream("DiscordMonies.Resources.2houses.png")!;
        using var house3Stream = asm.GetManifestResourceStream("DiscordMonies.Resources.3houses.png")!;
        using var house4Stream = asm.GetManifestResourceStream("DiscordMonies.Resources.4houses.png")!;
        using var hotelStream = asm.GetManifestResourceStream("DiscordMonies.Resources.hotel.png")!;
        using var arrowStream = asm.GetManifestResourceStream("DiscordMonies.Resources.arrow.png")!;
       
        _baseBoard = SKBitmap.Decode(boardStream);
        _basePiece = SKBitmap.Decode(pieceStream);
        _owned = SKBitmap.Decode(ownedStream);
        _mortgaged = SKBitmap.Decode(mortgagedStream);
        _house1 = SKBitmap.Decode(house1Stream);
        _house2 = SKBitmap.Decode(house2Stream);
        _house3 = SKBitmap.Decode(house3Stream);
        _house4 = SKBitmap.Decode(house4Stream);
        _hotel = SKBitmap.Decode(hotelStream);
        _arrow = SKBitmap.Decode(arrowStream);
    }

    public FileData RenderToRemoraAttachment(IEnumerable<HighlightedSpace> highlightedSpaces) => new("board.png", RenderToStream(highlightedSpaces));

    public MemoryStream RenderToStream(IEnumerable<HighlightedSpace> highlightedSpaces)
    {
        using var bmp = Render(highlightedSpaces);
        var memoryStream = new MemoryStream();
        bmp.Encode(memoryStream, SKEncodedImageFormat.Png, 100);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    public SKBitmap Render(IEnumerable<HighlightedSpace> highlightedSpaces)
    {
        var bmp = _baseBoard.Copy(); // dont need to using because we are returning this
        var canvas = new SKCanvas(bmp);

        foreach (var space in _state.Board.Spaces)
        {
            if (space.Info is not PropertySpace ps || space.Owner is null)
            {
                continue;
            }
            
            var pos = new SKPoint(ps.Bounds.X + 5, ps.Bounds.Y + 5);
            var color = space.Owner.Color.Color;
            
            if (space.IsMortgaged)
            {
                using var c = Colored(_mortgaged, color);
                canvas.DrawBitmap(c, pos);
                continue;
            }

            if (ps is RoadSpace)
            {
                var piece = space.BuiltHouses switch
                {
                    1 => _house1,
                    2 => _house2,
                    3 => _house3,
                    4 => _house4,
                    5 => _hotel,
                    _ => _owned,
                };
                using var c = Colored(piece, color);
                canvas.DrawBitmap(c, pos);
                continue;
            }

            using var col = Colored(_owned, color);
            canvas.DrawBitmap(col, pos);
        }

        foreach (var player in _state.Players)
        {
            var bounds = player.JailTurns != -1 ?
                _state.Board.JailBounds :
                player.CurrentSpace.Info.Bounds;

            var hash = HashCode.Combine(player.Id, bounds);
            var xOff = (hash & 0xFF) / 255.0;
            var yOff = ((hash >> 8) & 0xFF) / 255.0;
            var xPos = (int)(xOff * (bounds.Width - _basePiece.Width) + (bounds.X + _basePiece.Width / 2));
            var yPos = (int)(yOff * (bounds.Height - _basePiece.Height) + (bounds.Y + _basePiece.Height / 2));
            using var c = Colored(_basePiece, player.Color.Color);
            canvas.DrawBitmap(c, new SKPoint(xPos - _basePiece.Height / 2, yPos - _basePiece.Height / 2));
        }

        foreach (var space in highlightedSpaces)
        {
            List<int> sides = [space.Space.Index / 10];
            var rotation = sides[0] * 90;
            if (space.Space.Index % 10 == 0)
            {
                rotation -= 45;
                sides.Add((sides[0] + 3) % 4);
            }
            var arrow = Colored(RotateBitmap(_arrow, rotation), space.Color);

            var bounds = space.Space.Info.Bounds;
            if (space.Space.Info is JailSpace)
            {
                bounds = space.Space.Board.JailBounds;
            }
            var center = new SKPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var margin = sides.Count == 1 ? 100 : 50;
            foreach (var side in sides)
            {
                center.X += side switch
                {
                    0 => 0,
                    1 => -bounds.Width / 2 - margin,
                    2 => 0,
                    3 => bounds.Width / 2 + margin,
                    _ => 0
                };
                center.Y += side switch
                {
                    0 => bounds.Height / 2 + margin,
                    1 => 0,
                    2 => -bounds.Height / 2 - margin,
                    3 => 0,
                    _ => 0
                };
            }

            center.X -= arrow.Width / 2;
            center.Y -= arrow.Height / 2;
            
            canvas.DrawBitmap(arrow, center);
        }

        return bmp;
    }

    private static SKBitmap Colored(SKBitmap bitmap, Color color)
    {
        var bmp = bitmap.Copy();
        var cnv = new SKCanvas(bmp);
        cnv.DrawColor(new(color.R, color.G, color.B), SKBlendMode.Modulate);
        return bmp;
    }

    private static SKBitmap RotateBitmap(SKBitmap oldBitmap, float angle)
    {
        // Get the dimensions of the bitmap.
        var oldWidth = oldBitmap.Width;
        var oldHeight = oldBitmap.Height;

        // Calculate the rotation angle in radians
        var radians = double.Pi * angle / 180;

        // Calculate new dimensions of the rotated bitmap
        var cos = double.Abs(double.Cos(radians));
        var sin = double.Abs(double.Sin(radians));
        var newWidth = (int)double.Floor(oldWidth * cos + oldHeight * sin);
        var newHeight = (int)double.Floor(oldHeight * cos + oldWidth * sin);

        // Create a new empty bitmap with the new dimensions
        var newBitmap = new SKBitmap(newWidth, newHeight);

        // Create a new canvas, which we'll draw on.
        using (var canvas = new SKCanvas(newBitmap))
        {
            canvas.Clear(SKColors.Transparent);

            // Put the rotation point in the center of the image
            float px = newWidth / 2;
            float py = newHeight / 2;

            // Now we just apply our transformation,
            // which will do the rotation and scaling
            canvas.Translate(px, py);
            canvas.RotateDegrees(angle);
            canvas.Translate(-oldWidth / 2, -oldHeight / 2);

            // And then we draw the old bitmap onto our canvas
            canvas.DrawBitmap(oldBitmap, 0, 0);

            // restore the canvas state
            canvas.Restore();
        }

        return newBitmap;
    }

    public void Dispose()
    {
        _baseBoard.Dispose();
        _basePiece.Dispose();
        _owned.Dispose();
        _mortgaged.Dispose();
        _house1.Dispose();
        _house2.Dispose();
        _house3.Dispose();
        _house4.Dispose();
        _hotel.Dispose();
        GC.SuppressFinalize(this);
    }
}