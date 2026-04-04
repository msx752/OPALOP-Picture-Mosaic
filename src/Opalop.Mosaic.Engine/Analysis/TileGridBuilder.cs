namespace Opalop.Mosaic.Engine.Analysis;

using Opalop.Domain.ValueObjects;
using SkiaSharp;

public record TileInfo(int Index, int X, int Y, ColorFingerprint Fingerprint);

public static class TileGridBuilder
{
    public static IReadOnlyList<TileInfo> Build(SKBitmap source, int tileSize, float blurSigma = 5f)
    {
        using var working = blurSigma > 0 ? ApplyBlur(source, blurSigma) : source.Copy();

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
