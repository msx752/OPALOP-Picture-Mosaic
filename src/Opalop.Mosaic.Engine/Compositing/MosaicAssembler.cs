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

        int baseOpacityPercent = (int)Math.Round(opacity / 255.0 * 100);
        var sourceSpan = result.GetPixelSpan();

        foreach (var tile in tiles)
        {
            // Luminance-adaptive opacity: sample source region's average brightness
            int adaptiveOpacity = CalculateAdaptiveOpacity(sourceSpan, result.Width,
                tile.X, tile.Y, tile.Bitmap.Width, tile.Bitmap.Height, baseOpacityPercent);

            using var transparentTile = TileCompositor.ApplyLegacyTransparency(tile.Bitmap, adaptiveOpacity);
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
    /// Calculates per-tile opacity based on source region luminance.
    /// Dark regions → higher opacity (tiles more visible).
    /// Bright regions → lower opacity (original more visible).
    /// </summary>
    private static int CalculateAdaptiveOpacity(ReadOnlySpan<byte> sourcePixels, int stride,
        int tileX, int tileY, int tileW, int tileH, int baseOpacity)
    {
        // Sample center pixel of tile region for speed (full average is expensive)
        int cx = tileX + tileW / 2;
        int cy = tileY + tileH / 2;
        int idx = (cy * stride + cx) * 4; // BGRA

        if (idx + 2 >= sourcePixels.Length) return baseOpacity;

        // Approximate luminance from RGB (BT.709)
        float luminance = (sourcePixels[idx + 2] * 0.2126f + sourcePixels[idx + 1] * 0.7152f + sourcePixels[idx] * 0.0722f) / 255f;

        // Dark (L≈0) → opacity * 1.3, Bright (L≈1) → opacity * 0.7
        float factor = 1.3f - 0.6f * luminance;
        return Math.Clamp((int)(baseOpacity * factor), 10, 95);
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
