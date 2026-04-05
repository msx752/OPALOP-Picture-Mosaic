namespace Opalop.Mosaic.Engine.Analysis;

using Opalop.Domain.ValueObjects;
using Opalop.Mosaic.Engine.ColorSpace;
using SkiaSharp;

public static class QuadrantAnalyzer
{
    private const int GridSize = 3; // 3x3 = 9 regions

    public static ColorFingerprint Analyze(SKBitmap bitmap)
    {
        int w = bitmap.Width;
        int h = bitmap.Height;
        var pixels = bitmap.GetPixelSpan();

        var regions = new QuadrantLab[GridSize * GridSize];
        int cellW = w / GridSize;
        int cellH = h / GridSize;

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                int startX = col * cellW;
                int startY = row * cellH;
                int regionW = (col == GridSize - 1) ? w - startX : cellW;
                int regionH = (row == GridSize - 1) ? h - startY : cellH;
                regions[row * GridSize + col] = AverageRegion(pixels, w, startX, startY, regionW, regionH);
            }
        }

        var total = AverageRegion(pixels, w, 0, 0, w, h);
        return new ColorFingerprint(total, regions);
    }

    private static QuadrantLab AverageRegion(ReadOnlySpan<byte> pixels, int stride,
        int startX, int startY, int width, int height)
    {
        long sumR = 0, sumG = 0, sumB = 0;
        int count = 0;
        int bytesPerPixel = 4; // BGRA

        for (int y = startY; y < startY + height; y++)
        {
            int rowOffset = y * stride * bytesPerPixel;
            for (int x = startX; x < startX + width; x++)
            {
                int i = rowOffset + x * bytesPerPixel;
                sumB += pixels[i];
                sumG += pixels[i + 1];
                sumR += pixels[i + 2];
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
