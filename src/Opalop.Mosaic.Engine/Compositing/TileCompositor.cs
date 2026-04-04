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
}
