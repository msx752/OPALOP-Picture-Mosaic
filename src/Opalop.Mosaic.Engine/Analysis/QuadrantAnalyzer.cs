namespace Opalop.Mosaic.Engine.Analysis;

using Opalop.Domain.ValueObjects;
using Opalop.Mosaic.Engine.ColorSpace;
using SkiaSharp;

public static class QuadrantAnalyzer
{
    public static ColorFingerprint Analyze(SKBitmap bitmap)
    {
        int halfW = bitmap.Width / 2;
        int halfH = bitmap.Height / 2;

        var topLeft = AverageRegion(bitmap, 0, 0, halfW, halfH);
        var topRight = AverageRegion(bitmap, halfW, 0, bitmap.Width - halfW, halfH);
        var bottomLeft = AverageRegion(bitmap, 0, halfH, halfW, bitmap.Height - halfH);
        var bottomRight = AverageRegion(bitmap, halfW, halfH, bitmap.Width - halfW, bitmap.Height - halfH);
        var total = AverageRegion(bitmap, 0, 0, bitmap.Width, bitmap.Height);

        return new ColorFingerprint(total, topLeft, topRight, bottomLeft, bottomRight);
    }

    private static QuadrantLab AverageRegion(SKBitmap bitmap, int startX, int startY, int width, int height)
    {
        long sumR = 0, sumG = 0, sumB = 0;
        int count = 0;

        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                sumR += pixel.Red;
                sumG += pixel.Green;
                sumB += pixel.Blue;
                count++;
            }
        }

        if (count == 0) return new QuadrantLab(0, 0, 0);

        byte avgR = (byte)(sumR / count);
        byte avgG = (byte)(sumG / count);
        byte avgB = (byte)(sumB / count);

        return LabConverter.RgbToLab(avgR, avgG, avgB);
    }
}
