namespace Opalop.Domain.ValueObjects;

public readonly record struct ColorFingerprint(
    QuadrantLab Total, QuadrantLab TopLeft, QuadrantLab TopRight,
    QuadrantLab BottomLeft, QuadrantLab BottomRight)
{
    /// <summary>CIE76 weighted — fast, used for pre-filtering.</summary>
    public float WeightedDeltaE(ColorFingerprint other)
    {
        return 0.4f * Total.DeltaE(other.Total)
             + 0.15f * TopLeft.DeltaE(other.TopLeft)
             + 0.15f * TopRight.DeltaE(other.TopRight)
             + 0.15f * BottomLeft.DeltaE(other.BottomLeft)
             + 0.15f * BottomRight.DeltaE(other.BottomRight);
    }

    /// <summary>CIEDE2000 weighted — perceptually accurate, used for final ranking.</summary>
    public float WeightedDeltaE2000(ColorFingerprint other)
    {
        return 0.4f * Total.DeltaE2000(other.Total)
             + 0.15f * TopLeft.DeltaE2000(other.TopLeft)
             + 0.15f * TopRight.DeltaE2000(other.TopRight)
             + 0.15f * BottomLeft.DeltaE2000(other.BottomLeft)
             + 0.15f * BottomRight.DeltaE2000(other.BottomRight);
    }
}
