namespace Opalop.Mosaic.Engine.Compositing;

using SkiaSharp;

public record ProcessedTile(int X, int Y, SKBitmap Bitmap) : IDisposable
{
    public void Dispose() => Bitmap.Dispose();
}

public static class MosaicAssembler
{
    public static SKBitmap Assemble(IReadOnlyList<ProcessedTile> tiles,
        int canvasWidth, int canvasHeight, int tileSize)
    {
        var result = new SKBitmap(canvasWidth, canvasHeight);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);

        foreach (var tile in tiles)
            canvas.DrawBitmap(tile.Bitmap, tile.X, tile.Y);

        return result;
    }
}
