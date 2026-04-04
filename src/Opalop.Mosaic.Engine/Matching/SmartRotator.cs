namespace Opalop.Mosaic.Engine.Matching;

using Opalop.Domain.ValueObjects;

public enum RotationAngle { None = 0, Rotate90 = 90, Rotate180 = 180, Rotate270 = 270 }

public static class SmartRotator
{
    public static RotationAngle DetermineRotation(ColorFingerprint target, ColorFingerprint candidate)
    {
        var targetQuadrants = new (QuadrantLab Lab, int Position)[]
        {
            (target.TopLeft, 0), (target.TopRight, 1),
            (target.BottomLeft, 2), (target.BottomRight, 3)
        };

        // Find target's most distinctive quadrant (farthest from total average)
        var mostDistinctive = targetQuadrants
            .OrderByDescending(q => q.Lab.DeltaE(target.Total))
            .First();

        var candidateQuadrants = new (QuadrantLab Lab, int Position)[]
        {
            (candidate.TopLeft, 0), (candidate.TopRight, 1),
            (candidate.BottomLeft, 2), (candidate.BottomRight, 3)
        };

        // Find candidate quadrant closest to target's distinctive quadrant
        var bestMatch = candidateQuadrants
            .OrderBy(q => q.Lab.DeltaE(mostDistinctive.Lab))
            .First();

        return GetRotation(bestMatch.Position, mostDistinctive.Position);
    }

    // Rotation lookup: [from][to] -> angle needed
    // Positions: 0=TL, 1=TR, 2=BL, 3=BR
    // 90 deg CW: TL->TR, TR->BR, BR->BL, BL->TL
    private static RotationAngle GetRotation(int from, int to)
    {
        int[,] table =
        {
            { 0, 90, 270, 180 },    // from TL
            { 270, 0, 180, 90 },    // from TR
            { 90, 180, 0, 270 },    // from BL
            { 180, 270, 90, 0 }     // from BR
        };
        return (RotationAngle)table[from, to];
    }
}
