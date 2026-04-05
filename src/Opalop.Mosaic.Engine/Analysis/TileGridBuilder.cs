namespace Opalop.Mosaic.Engine.Analysis;

using Opalop.Domain.ValueObjects;
using SkiaSharp;

public record TileInfo(int Index, int X, int Y, ColorFingerprint Fingerprint);

/// <summary>
/// Output dimensions after upscale + tile-alignment.
/// </summary>
public record GridDimensions(int Width, int Height);

public static class TileGridBuilder
{
    /// <summary>
    /// Upscale percentage applied to the source image before tiling.
    /// 100 = double the resolution (legacy default), giving 4x more tiles.
    /// </summary>
    private const int DefaultUpscalePercent = 100;

    public static IReadOnlyList<TileInfo> Build(SKBitmap source, int tileSize,
        int upscalePercent = DefaultUpscalePercent, float blurSigma = 5f)
    {
        // Legacy logic: enlarge source so smaller tiles fit, then snap to tile-aligned dimensions
        using var upscaled = Upscale(source, tileSize, upscalePercent);
        using var working = blurSigma > 0 ? ApplyBlur(upscaled, blurSigma) : upscaled.Copy();

        int cols = working.Width / tileSize;
        int rows = working.Height / tileSize;
        var tiles = new List<TileInfo>(cols * rows);
        int index = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int x = col * tileSize;
                int y = row * tileSize;

                using var tileBitmap = new SKBitmap(tileSize, tileSize);
                using var canvas = new SKCanvas(tileBitmap);
                canvas.DrawBitmap(working, SKRect.Create(x, y, tileSize, tileSize),
                    SKRect.Create(0, 0, tileSize, tileSize));

                var fingerprint = QuadrantAnalyzer.Analyze(tileBitmap);
                tiles.Add(new TileInfo(index++, x, y, fingerprint));
            }
        }

        return tiles;
    }

    /// <summary>
    /// Calculates the output canvas dimensions after upscale + tile alignment.
    /// Used by the assembler to size the final mosaic.
    /// </summary>
    public static GridDimensions CalculateDimensions(int sourceWidth, int sourceHeight,
        int tileSize, int upscalePercent = DefaultUpscalePercent)
    {
        int w = sourceWidth + (int)(sourceWidth * upscalePercent / 100.0);
        int h = sourceHeight + (int)(sourceHeight * upscalePercent / 100.0);
        int alignedW = w - (w % tileSize);
        int alignedH = h - (h % tileSize);
        return new GridDimensions(alignedW, alignedH);
    }

    /// <summary>
    /// Upscales the source image and snaps dimensions to tile-size multiples.
    /// Legacy: yuzde=100 → 2x enlargement, then trim to nearest tile boundary.
    /// </summary>
    private static SKBitmap Upscale(SKBitmap source, int tileSize, int upscalePercent)
    {
        if (upscalePercent <= 0)
        {
            // No upscale — just align to tile boundary
            int alignedW = source.Width - (source.Width % tileSize);
            int alignedH = source.Height - (source.Height % tileSize);
            return source.Resize(new SKImageInfo(alignedW, alignedH), SKSamplingOptions.Default)
                   ?? source.Copy();
        }

        int w = source.Width + (int)(source.Width * upscalePercent / 100.0);
        int h = source.Height + (int)(source.Height * upscalePercent / 100.0);
        int newW = w - (w % tileSize);
        int newH = h - (h % tileSize);

        return source.Resize(new SKImageInfo(newW, newH), SKSamplingOptions.Default)
               ?? source.Copy();
    }

    private static SKBitmap ApplyBlur(SKBitmap source, float sigma)
    {
        var blurred = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(blurred);
        using var paint = new SKPaint();
        paint.ImageFilter = SKImageFilter.CreateBlur(sigma, sigma);
        canvas.DrawBitmap(source, 0, 0, paint);
        return blurred;
    }
}
