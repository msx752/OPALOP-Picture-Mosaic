namespace Opalop.Domain.ValueObjects;

/// <summary>
/// Color fingerprint using a 3x3 grid (9 regions) + 1 total average.
/// Regions numbered left-to-right, top-to-bottom:
///   R0 R1 R2
///   R3 R4 R5
///   R6 R7 R8
/// </summary>
public readonly record struct ColorFingerprint
{
    public QuadrantLab Total { get; init; }
    public QuadrantLab[] Regions { get; init; }

    /// <summary>3x3 grid constructor (9 regions + total).</summary>
    public ColorFingerprint(QuadrantLab total, QuadrantLab[] regions)
    {
        Total = total;
        Regions = regions;
    }

    /// <summary>Legacy 4-quadrant constructor — maps to 3x3 by filling center/edges from quadrants.</summary>
    public ColorFingerprint(QuadrantLab total, QuadrantLab topLeft, QuadrantLab topRight,
        QuadrantLab bottomLeft, QuadrantLab bottomRight)
    {
        Total = total;
        // Map 4 quadrants to 9 regions: corners get quadrant values, edges/center get averages
        var midTop = Avg(topLeft, topRight);
        var midLeft = Avg(topLeft, bottomLeft);
        var midRight = Avg(topRight, bottomRight);
        var midBottom = Avg(bottomLeft, bottomRight);
        Regions = [topLeft, midTop, topRight, midLeft, total, midRight, bottomLeft, midBottom, bottomRight];
    }

    // Convenience accessors for SmartRotator compatibility
    public QuadrantLab TopLeft => Regions[0];
    public QuadrantLab TopRight => Regions[2];
    public QuadrantLab BottomLeft => Regions[6];
    public QuadrantLab BottomRight => Regions[8];

    /// <summary>CIE76 weighted — fast, used for pre-filtering.</summary>
    public float WeightedDeltaE(ColorFingerprint other)
    {
        float sum = 0.30f * Total.DeltaE(other.Total);
        float regionWeight = 0.70f / Regions.Length;
        for (int i = 0; i < Regions.Length && i < other.Regions.Length; i++)
            sum += regionWeight * Regions[i].DeltaE(other.Regions[i]);
        return sum;
    }

    /// <summary>CIEDE2000 weighted — perceptually accurate, used for final ranking.</summary>
    public float WeightedDeltaE2000(ColorFingerprint other)
    {
        float sum = 0.30f * Total.DeltaE2000(other.Total);
        float regionWeight = 0.70f / Regions.Length;
        for (int i = 0; i < Regions.Length && i < other.Regions.Length; i++)
            sum += regionWeight * Regions[i].DeltaE2000(other.Regions[i]);
        return sum;
    }

    private static QuadrantLab Avg(QuadrantLab a, QuadrantLab b) =>
        new((a.L + b.L) / 2f, (a.A + b.A) / 2f, (a.B + b.B) / 2f);
}
