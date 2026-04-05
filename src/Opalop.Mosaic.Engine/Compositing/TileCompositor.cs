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
    /// </summary>
    public static SKBitmap ApplyLegacyTransparency(SKBitmap source, int opacityPercent)
    {
        var result = new SKBitmap(source.Width, source.Height);
        float factor = opacityPercent / 100f;

        for (int x = 0; x < source.Width; x++)
        {
            for (int y = 0; y < source.Height; y++)
            {
                var c = source.GetPixel(x, y);
                if (c.Alpha == 0) continue;

                var newColor = new SKColor(
                    (byte)(c.Red * factor),
                    (byte)(c.Green * factor),
                    (byte)(c.Blue * factor),
                    (byte)(c.Alpha * factor));
                result.SetPixel(x, y, newColor);
            }
        }

        return result;
    }
}
