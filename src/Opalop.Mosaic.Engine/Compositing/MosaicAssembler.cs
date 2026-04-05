namespace Opalop.Mosaic.Engine.Compositing;

using SkiaSharp;

public record ProcessedTile(int X, int Y, SKBitmap Bitmap) : IDisposable
{
    public void Dispose() => Bitmap.Dispose();
}

public static class MosaicAssembler
{
    /// <summary>
    /// Assembles the mosaic using legacy premultiplied-alpha compositing:
    /// 1. Source image is resized to match the upscaled tile grid
    /// 2. Each tile gets legacy transparency (both RGB + Alpha scaled by opacity%)
    /// 3. Tiles are drawn on top of the source image
    /// This matches the original GDI+ Transparnt() + DrawImage behavior.
    /// </summary>
    public static SKBitmap Assemble(IReadOnlyList<ProcessedTile> tiles,
        SKBitmap sourceImage, int canvasWidth, int canvasHeight, byte opacity)
    {
        var result = sourceImage.Resize(new SKImageInfo(canvasWidth, canvasHeight), SKSamplingOptions.Default)
                     ?? sourceImage.Copy();
        using var canvas = new SKCanvas(result);

        // Legacy opacity is 0-100 percent; convert from 0-255 byte
        int opacityPercent = (int)Math.Round(opacity / 255.0 * 100);

        foreach (var tile in tiles)
        {
            using var transparentTile = TileCompositor.ApplyLegacyTransparency(tile.Bitmap, opacityPercent);
            canvas.DrawBitmap(transparentTile, tile.X, tile.Y);
        }

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
