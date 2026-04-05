namespace Opalop.Mosaic.Engine.Compositing;

using Opalop.Mosaic.Engine.Matching;
using SkiaSharp;

public static class TileCompositor
{
    public static void Composite(SKBitmap canvas, SKBitmap tile, int x, int y,
        RotationAngle rotation, byte opacity = 128)
    {
        using var surface = new SKCanvas(canvas);

        surface.Save();
        if (rotation != RotationAngle.None)
        {
            float cx = x + tile.Width / 2f;
            float cy = y + tile.Height / 2f;
            surface.RotateDegrees((float)rotation, cx, cy);
        }

        using var paint = new SKPaint { Color = new SKColor(255, 255, 255, opacity) };
        surface.DrawBitmap(tile, x, y, paint);
        surface.Restore();
    }

    /// <summary>
    /// Applies legacy premultiplied-alpha transparency to a tile bitmap.
    /// Scales both Alpha AND RGB channels by opacity/100, matching the original
    /// GDI+ Transparnt() behavior which dims colors alongside transparency.
    /// Uses direct pixel span access for performance (~50x faster than GetPixel/SetPixel).
    /// </summary>
    public static SKBitmap ApplyLegacyTransparency(SKBitmap source, int opacityPercent)
    {
        var result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        float factor = opacityPercent / 100f;

        var srcSpan = source.GetPixelSpan();
        var dstBytes = new byte[srcSpan.Length];

        for (int i = 0; i < srcSpan.Length; i += 4)
        {
            byte a = srcSpan[i + 3]; // BGRA: [B, G, R, A]
            if (a == 0) continue;

            dstBytes[i] = (byte)(srcSpan[i] * factor);       // B
            dstBytes[i + 1] = (byte)(srcSpan[i + 1] * factor); // G
            dstBytes[i + 2] = (byte)(srcSpan[i + 2] * factor); // R
            dstBytes[i + 3] = (byte)(a * factor);               // A
        }

        System.Runtime.InteropServices.Marshal.Copy(dstBytes, 0, result.GetPixels(), dstBytes.Length);
        return result;
    }
}
