namespace Opalop.Domain.Tests.ValueObjects;

using FluentAssertions;
using Opalop.Domain.ValueObjects;
using Xunit;

public class ColorFingerprintTests
{
    [Fact]
    public void DeltaE_IdenticalFingerprints_ReturnsZero()
    {
        var quad = new QuadrantLab(50.0f, 20.0f, -10.0f);
        var fp = new ColorFingerprint(quad, quad, quad, quad, quad);
        fp.WeightedDeltaE(fp).Should().Be(0f);
    }

    [Fact]
    public void DeltaE_DifferentFingerprints_ReturnsPositiveValue()
    {
        var fp1 = new ColorFingerprint(
            new QuadrantLab(50f, 20f, -10f),
            new QuadrantLab(55f, 22f, -8f),
            new QuadrantLab(45f, 18f, -12f),
            new QuadrantLab(52f, 21f, -9f),
            new QuadrantLab(48f, 19f, -11f));
        var fp2 = new ColorFingerprint(
            new QuadrantLab(70f, 10f, 5f),
            new QuadrantLab(75f, 12f, 7f),
            new QuadrantLab(65f, 8f, 3f),
            new QuadrantLab(72f, 11f, 6f),
            new QuadrantLab(68f, 9f, 4f));
        fp1.WeightedDeltaE(fp2).Should().BeGreaterThan(0f);
    }

    [Fact]
    public void QuadrantLab_DeltaE_CalculatesEuclideanDistance()
    {
        var a = new QuadrantLab(50f, 20f, 30f);
        var b = new QuadrantLab(50f, 20f, 30f);
        a.DeltaE(b).Should().Be(0f);

        var c = new QuadrantLab(53f, 24f, 30f);
        // sqrt(9 + 16 + 0) = 5
        a.DeltaE(c).Should().Be(5f);
    }
}
