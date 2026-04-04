namespace Opalop.Mosaic.Engine.Tests.Analysis;

using FluentAssertions;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;
using Xunit;

public class QuadrantAnalyzerTests
{
    [Fact]
    public void SolidColorBitmap_AllQuadrantsEqual()
    {
        using var bitmap = new SKBitmap(100, 100);
        bitmap.Erase(new SKColor(128, 64, 200));
        var fp = QuadrantAnalyzer.Analyze(bitmap);
        fp.TopLeft.L.Should().BeApproximately(fp.Total.L, 0.1f);
        fp.TopRight.L.Should().BeApproximately(fp.Total.L, 0.1f);
        fp.BottomLeft.L.Should().BeApproximately(fp.Total.L, 0.1f);
        fp.BottomRight.L.Should().BeApproximately(fp.Total.L, 0.1f);
    }

    [Fact]
    public void TwoColorBitmap_QuadrantsDiffer()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.DrawRect(0, 0, 100, 50, new SKPaint { Color = SKColors.Red });
        canvas.DrawRect(0, 50, 100, 50, new SKPaint { Color = SKColors.Blue });
        var fp = QuadrantAnalyzer.Analyze(bitmap);
        fp.TopLeft.A.Should().BeGreaterThan(fp.BottomLeft.A);
        fp.TopRight.A.Should().BeGreaterThan(fp.BottomRight.A);
    }

    [Fact]
    public void Fingerprint_HasMeaningfulValues()
    {
        using var bitmap = new SKBitmap(94, 94);
        bitmap.Erase(SKColors.Green);
        var fp = QuadrantAnalyzer.Analyze(bitmap);
        fp.Total.L.Should().BeGreaterThan(0f);
    }
}
