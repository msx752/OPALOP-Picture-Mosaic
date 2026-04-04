namespace Opalop.Mosaic.Engine.Tests.Analysis;

using FluentAssertions;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;
using Xunit;

public class TileGridBuilderTests
{
    [Fact]
    public void BuildGrid_CorrectTileCount()
    {
        using var bitmap = new SKBitmap(200, 200);
        bitmap.Erase(SKColors.Gray);
        var tiles = TileGridBuilder.Build(bitmap, tileSize: 100);
        tiles.Should().HaveCount(4);
    }

    [Fact]
    public void BuildGrid_TilePositionsCorrect()
    {
        using var bitmap = new SKBitmap(200, 200);
        bitmap.Erase(SKColors.Gray);
        var tiles = TileGridBuilder.Build(bitmap, tileSize: 100);
        tiles[0].X.Should().Be(0); tiles[0].Y.Should().Be(0);
        tiles[1].X.Should().Be(100); tiles[1].Y.Should().Be(0);
        tiles[2].X.Should().Be(0); tiles[2].Y.Should().Be(100);
        tiles[3].X.Should().Be(100); tiles[3].Y.Should().Be(100);
    }

    [Fact]
    public void BuildGrid_DiscardRemainderPixels()
    {
        using var bitmap = new SKBitmap(250, 250);
        bitmap.Erase(SKColors.Gray);
        var tiles = TileGridBuilder.Build(bitmap, tileSize: 100);
        tiles.Should().HaveCount(4);
    }

    [Fact]
    public void BuildGrid_EachTileHasFingerprint()
    {
        using var bitmap = new SKBitmap(200, 200);
        bitmap.Erase(new SKColor(100, 150, 200));
        var tiles = TileGridBuilder.Build(bitmap, tileSize: 100);
        foreach (var tile in tiles)
            tile.Fingerprint.Total.L.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildGrid_WithAndWithoutBlur_ProducesSameTileCount()
    {
        using var bitmap = new SKBitmap(200, 200);
        bitmap.Erase(SKColors.Gray);
        var tilesNoBlur = TileGridBuilder.Build(bitmap, tileSize: 100, blurSigma: 0);
        var tilesBlur = TileGridBuilder.Build(bitmap, tileSize: 100, blurSigma: 5);
        tilesNoBlur.Should().HaveCount(4);
        tilesBlur.Should().HaveCount(4);
    }
}
