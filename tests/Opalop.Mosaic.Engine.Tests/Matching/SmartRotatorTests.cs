namespace Opalop.Mosaic.Engine.Tests.Matching;

using FluentAssertions;
using Opalop.Domain.ValueObjects;
using Opalop.Mosaic.Engine.Matching;
using Xunit;

public class SmartRotatorTests
{
    [Fact]
    public void IdenticalQuadrants_ReturnsNoRotation()
    {
        var q = new QuadrantLab(50, 20, -10);
        var fp = new ColorFingerprint(q, q, q, q, q);
        SmartRotator.DetermineRotation(target: fp, candidate: fp).Should().Be(RotationAngle.None);
    }

    [Fact]
    public void BrightTopLeft_MatchesBrightTopLeft_NoRotation()
    {
        var bright = new QuadrantLab(90, 10, 5);
        var dark = new QuadrantLab(20, 5, -5);
        var mid = new QuadrantLab(50, 8, 0);
        var target = new ColorFingerprint(mid, bright, dark, dark, dark);
        var candidate = new ColorFingerprint(mid, bright, dark, dark, dark);
        SmartRotator.DetermineRotation(target, candidate).Should().Be(RotationAngle.None);
    }

    [Fact]
    public void BrightTopLeft_MatchesBrightTopRight_Rotates270()
    {
        var bright = new QuadrantLab(90, 10, 5);
        var dark = new QuadrantLab(20, 5, -5);
        var mid = new QuadrantLab(50, 8, 0);
        var target = new ColorFingerprint(mid, bright, dark, dark, dark);
        var candidate = new ColorFingerprint(mid, dark, bright, dark, dark);
        SmartRotator.DetermineRotation(target, candidate).Should().Be(RotationAngle.Rotate270);
    }

    [Fact]
    public void BrightTopLeft_MatchesBrightBottomLeft_Rotates90()
    {
        var bright = new QuadrantLab(90, 10, 5);
        var dark = new QuadrantLab(20, 5, -5);
        var mid = new QuadrantLab(50, 8, 0);
        var target = new ColorFingerprint(mid, bright, dark, dark, dark);
        var candidate = new ColorFingerprint(mid, dark, dark, bright, dark);
        SmartRotator.DetermineRotation(target, candidate).Should().Be(RotationAngle.Rotate90);
    }

    [Fact]
    public void BrightTopLeft_MatchesBrightBottomRight_Rotates180()
    {
        var bright = new QuadrantLab(90, 10, 5);
        var dark = new QuadrantLab(20, 5, -5);
        var mid = new QuadrantLab(50, 8, 0);
        var target = new ColorFingerprint(mid, bright, dark, dark, dark);
        var candidate = new ColorFingerprint(mid, dark, dark, dark, bright);
        SmartRotator.DetermineRotation(target, candidate).Should().Be(RotationAngle.Rotate180);
    }
}
