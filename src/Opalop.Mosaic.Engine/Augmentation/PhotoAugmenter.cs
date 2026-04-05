namespace Opalop.Mosaic.Engine.Augmentation;

using SkiaSharp;

/// <summary>
/// Generates color-shifted and flipped variants of photos to expand
/// the effective library size 3-5x, improving color coverage.
/// </summary>
public static class PhotoAugmenter
{
    public record AugmentedVariant(SKBitmap Bitmap, string Suffix);

    /// <summary>
    /// Creates augmented variants: brightness shifts (±15%), horizontal flip.
    /// Returns the variants (caller must dispose bitmaps).
    /// </summary>
    public static List<AugmentedVariant> Generate(SKBitmap source)
    {
        var variants = new List<AugmentedVariant>();

        // Brightness +15%
        variants.Add(new(AdjustBrightness(source, 1.15f), "bright"));

        // Brightness -15%
        variants.Add(new(AdjustBrightness(source, 0.85f), "dark"));

        // Horizontal flip
        var flipped = new SKBitmap(source.Width, source.Height);
        using (var canvas = new SKCanvas(flipped))
        {
            canvas.Scale(-1, 1, source.Width / 2f, source.Height / 2f);
            canvas.DrawBitmap(source, 0, 0);
        }
        variants.Add(new(flipped, "flip"));

        return variants;
    }

    private static SKBitmap AdjustBrightness(SKBitmap source, float factor)
    {
        var result = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(result);

        var matrix = new float[]
        {
            factor, 0, 0, 0, 0,
            0, factor, 0, 0, 0,
            0, 0, factor, 0, 0,
            0, 0, 0, 1, 0,
        };

        using var paint = new SKPaint();
        paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0, paint);

        return result;
    }
}
