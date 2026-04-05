namespace Opalop.Mosaic.Engine.Tests.Compositing;

using FluentAssertions;
using Opalop.Mosaic.Engine.Compositing;
using Opalop.Mosaic.Engine.Matching;
using SkiaSharp;
using Xunit;

public class MosaicAssemblerTests
{
    [Fact]
    public void Composite_DrawsTileAtCorrectPosition()
    {
        using var canvas = new SKBitmap(200, 200);
        canvas.Erase(SKColors.White);
        using var tile = new SKBitmap(100, 100);
        tile.Erase(SKColors.Red);

        TileCompositor.Composite(canvas, tile, x: 100, y: 0, RotationAngle.None, opacity: 255);

        var pixel = canvas.GetPixel(150, 50);
        pixel.Red.Should().BeGreaterThan(200);
    }

    [Fact]
    public void Composite_AppliesAlpha()
    {
        using var canvas = new SKBitmap(100, 100);
        canvas.Erase(SKColors.White);
        using var tile = new SKBitmap(100, 100);
        tile.Erase(SKColors.Black);

        TileCompositor.Composite(canvas, tile, 0, 0, RotationAngle.None, opacity: 128);

        var pixel = canvas.GetPixel(50, 50);
        pixel.Red.Should().BeInRange(100, 160); // white + semi-transparent black = gray
    }

    [Fact]
    public void Composite_AppliesRotation180()
    {
        using var canvas = new SKBitmap(100, 100);
        canvas.Erase(SKColors.White);
        using var tile = new SKBitmap(100, 100);
        using var tileCanvas = new SKCanvas(tile);
        tileCanvas.DrawRect(0, 0, 100, 50, new SKPaint { Color = SKColors.Red });
        tileCanvas.DrawRect(0, 50, 100, 50, new SKPaint { Color = SKColors.Blue });

        TileCompositor.Composite(canvas, tile, 0, 0, RotationAngle.Rotate180, opacity: 255);

        // After 180° rotation: top should be blue, bottom should be red
        var topPixel = canvas.GetPixel(50, 25);
        var bottomPixel = canvas.GetPixel(50, 75);
        topPixel.Blue.Should().BeGreaterThan(topPixel.Red);
        bottomPixel.Red.Should().BeGreaterThan(bottomPixel.Blue);
    }

    [Fact]
    public void Assemble_CreatesCorrectSizeCanvas()
    {
        var tiles = new List<ProcessedTile>
        {
            CreateTile(0, 0, 100, SKColors.Red),
            CreateTile(100, 0, 100, SKColors.Green),
            CreateTile(0, 100, 100, SKColors.Blue),
            CreateTile(100, 100, 100, SKColors.Yellow)
        };

        using var result = MosaicAssembler.Assemble(tiles, 200, 200);

        result.Width.Should().Be(200);
        result.Height.Should().Be(200);
        foreach (var t in tiles) t.Dispose();
    }

    [Fact]
    public void Assemble_PlacesTilesCorrectly()
    {
        var tiles = new List<ProcessedTile>
        {
            CreateTile(0, 0, 100, SKColors.Red),
            CreateTile(100, 0, 100, SKColors.Blue)
        };

        using var result = MosaicAssembler.Assemble(tiles, 200, 100);

        result.GetPixel(50, 50).Red.Should().BeGreaterThan(200);
        result.GetPixel(150, 50).Blue.Should().BeGreaterThan(200);
        foreach (var t in tiles) t.Dispose();
    }

    private static ProcessedTile CreateTile(int x, int y, int size, SKColor color)
    {
        var bitmap = new SKBitmap(size, size);
        bitmap.Erase(color);
        return new ProcessedTile(x, y, bitmap);
    }
}
