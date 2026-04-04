namespace Opalop.Mosaic.Engine.Tests.ColorSpace;

using FluentAssertions;
using Opalop.Domain.ValueObjects;
using Opalop.Mosaic.Engine.ColorSpace;
using Xunit;

public class LabConverterTests
{
    [Fact]
    public void PureWhite_ReturnsL100()
    {
        var lab = LabConverter.RgbToLab(255, 255, 255);
        lab.L.Should().BeApproximately(100f, 0.5f);
        lab.A.Should().BeApproximately(0f, 1f);
        lab.B.Should().BeApproximately(0f, 1f);
    }

    [Fact]
    public void PureBlack_ReturnsL0()
    {
        var lab = LabConverter.RgbToLab(0, 0, 0);
        lab.L.Should().BeApproximately(0f, 0.5f);
        lab.A.Should().BeApproximately(0f, 1f);
        lab.B.Should().BeApproximately(0f, 1f);
    }

    [Fact]
    public void PureRed_ReturnsPositiveA()
    {
        var lab = LabConverter.RgbToLab(255, 0, 0);
        lab.L.Should().BeGreaterThan(40f);
        lab.A.Should().BeGreaterThan(50f);
        lab.B.Should().BeGreaterThan(30f);
    }

    [Fact]
    public void PureGreen_ReturnsNegativeA()
    {
        var lab = LabConverter.RgbToLab(0, 128, 0);
        lab.A.Should().BeLessThan(-20f);
    }

    [Fact]
    public void PureBlue_ReturnsNegativeB()
    {
        var lab = LabConverter.RgbToLab(0, 0, 255);
        lab.B.Should().BeLessThan(-50f);
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var lab = LabConverter.RgbToLab(128, 64, 200);
        var (r, g, b) = LabConverter.LabToRgb(lab);
        r.Should().BeCloseTo((byte)128, 2);
        g.Should().BeCloseTo((byte)64, 2);
        b.Should().BeCloseTo((byte)200, 2);
    }
}
