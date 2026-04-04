namespace Opalop.Domain.ValueObjects;

public readonly record struct ColorFingerprint(
    QuadrantLab Total, QuadrantLab TopLeft, QuadrantLab TopRight,
    QuadrantLab BottomLeft, QuadrantLab BottomRight)
{
    public float WeightedDeltaE(ColorFingerprint other)
    {
        return 0.4f * Total.DeltaE(other.Total)
             + 0.15f * TopLeft.DeltaE(other.TopLeft)
             + 0.15f * TopRight.DeltaE(other.TopRight)
             + 0.15f * BottomLeft.DeltaE(other.BottomLeft)
             + 0.15f * BottomRight.DeltaE(other.BottomRight);
    }
}
