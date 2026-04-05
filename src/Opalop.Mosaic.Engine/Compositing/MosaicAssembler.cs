namespace Opalop.Mosaic.Engine.Compositing;

using SkiaSharp;

public record ProcessedTile(int X, int Y, SKBitmap Bitmap) : IDisposable
{
    public void Dispose() => Bitmap.Dispose();
}

public static class MosaicAssembler
{
    /// <summary>
    /// Assembles the mosaic by drawing tiles on top of the original source image
    /// with configurable opacity, so the original photo shows through.
    /// The source image is resized to match the upscaled canvas dimensions.
    /// </summary>
    public static SKBitmap Assemble(IReadOnlyList<ProcessedTile> tiles,
        SKBitmap sourceImage, int canvasWidth, int canvasHeight, byte opacity)
    {
        // Resize source image to match the upscaled tile grid dimensions
        var result = sourceImage.Resize(new SKImageInfo(canvasWidth, canvasHeight), SKSamplingOptions.Default)
                     ?? sourceImage.Copy();
        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint { Color = new SKColor(255, 255, 255, opacity) };

        foreach (var tile in tiles)
            canvas.DrawBitmap(tile.Bitmap, tile.X, tile.Y, paint);

        return result;
    }

    /// <summary>
    /// Fallback: assembles on a blank canvas when source image is unavailable.
    /// </summary>
    public static SKBitmap Assemble(IReadOnlyList<ProcessedTile> tiles,
        int canvasWidth, int canvasHeight)
    {
        var result = new SKBitmap(canvasWidth, canvasHeight);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);

        foreach (var tile in tiles)
            canvas.DrawBitmap(tile.Bitmap, tile.X, tile.Y);

        return result;
    }

    /// <summary>
    /// Maps PixFormat tile size to default opacity (0-255).
    /// Smaller tiles = more tiles = original shows less, so opacity can be lower.
    /// </summary>
    public static byte GetDefaultOpacity(int pxFormat) => pxFormat switch
    {
        12 => 102,  // 40% — many tiny tiles, let original show more
        20 => 115,  // 45%
        36 => 128,  // 50% — balanced
        48 => 128,  // 50%
        64 => 140,  // 55% — fewer tiles, show them a bit more
        94 => 153,  // 60% — large tiles, show more of each photo
        _ => 128    // 50% default
    };
}
