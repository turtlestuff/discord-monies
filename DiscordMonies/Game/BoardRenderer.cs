using System.Drawing;
using DiscordMonies.Board;
using SkiaSharp;

namespace DiscordMonies.Game;

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
       
        _baseBoard = SKBitmap.Decode(boardStream);
        _basePiece = SKBitmap.Decode(pieceStream);
        _owned = SKBitmap.Decode(ownedStream);
        _mortgaged = SKBitmap.Decode(mortgagedStream);
        _house1 = SKBitmap.Decode(house1Stream);
        _house2 = SKBitmap.Decode(house2Stream);
        _house3 = SKBitmap.Decode(house3Stream);
        _house4 = SKBitmap.Decode(house4Stream);
        _hotel = SKBitmap.Decode(hotelStream);
    }

    public MemoryStream RenderToStream()
    {
        using var bmp = Render();
        var memoryStream = new MemoryStream();
        bmp.Encode(memoryStream, SKEncodedImageFormat.Png, 100);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    public SKBitmap Render()
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

        return bmp;
    }

    private static SKBitmap Colored(SKBitmap bitmap, Color color)
    {
        var bmp = bitmap.Copy();
        var cnv = new SKCanvas(bmp);
        cnv.DrawColor(new(color.R, color.G, color.B), SKBlendMode.Modulate);
        return bmp;
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