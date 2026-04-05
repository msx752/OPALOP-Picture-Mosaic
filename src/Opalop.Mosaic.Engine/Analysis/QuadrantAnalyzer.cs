namespace Opalop.Mosaic.Engine.Analysis;

using System.Runtime.InteropServices;
using Opalop.Domain.ValueObjects;
using Opalop.Mosaic.Engine.ColorSpace;
using SkiaSharp;

public static class QuadrantAnalyzer
{
    public static ColorFingerprint Analyze(SKBitmap bitmap)
    {
        int halfW = bitmap.Width / 2;
        int halfH = bitmap.Height / 2;
        int w = bitmap.Width;
        int h = bitmap.Height;

        var pixels = bitmap.GetPixelSpan();

        var topLeft = AverageRegion(pixels, w, 0, 0, halfW, halfH);
        var topRight = AverageRegion(pixels, w, halfW, 0, w - halfW, halfH);
        var bottomLeft = AverageRegion(pixels, w, 0, halfH, halfW, h - halfH);
        var bottomRight = AverageRegion(pixels, w, halfW, halfH, w - halfW, h - halfH);
        var total = AverageRegion(pixels, w, 0, 0, w, h);

        return new ColorFingerprint(total, topLeft, topRight, bottomLeft, bottomRight);
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
