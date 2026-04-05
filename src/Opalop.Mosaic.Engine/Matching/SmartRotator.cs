namespace Opalop.Mosaic.Engine.Matching;

using Opalop.Domain.ValueObjects;

public enum RotationAngle { None = 0, Rotate90 = 90, Rotate180 = 180, Rotate270 = 270 }

public static class SmartRotator
{
    private static readonly RotationAngle[] AllRotations =
        [RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270];

    // 3x3 grid index permutations for each rotation:
    // Original:  0 1 2    90° CW:   6 3 0    180°:     8 7 6    270° CW:  2 5 8
    //            3 4 5              7 4 1               5 4 3              1 4 7
    //            6 7 8              8 5 2               2 1 0              0 3 6
    private static readonly int[][] RotationMaps =
    [
        [0, 1, 2, 3, 4, 5, 6, 7, 8], // None
        [6, 3, 0, 7, 4, 1, 8, 5, 2], // 90° CW
        [8, 7, 6, 5, 4, 3, 2, 1, 0], // 180°
        [2, 5, 8, 1, 4, 7, 0, 3, 6], // 270° CW
    ];

    /// <summary>
    /// Evaluates all 4 rotation angles and picks the one with the lowest total
    /// WeightedDeltaE. Guarantees rotation never degrades match quality.
    /// </summary>
    public static RotationAngle DetermineRotation(ColorFingerprint target, ColorFingerprint candidate)
    {
        var bestRotation = RotationAngle.None;
        float bestScore = float.MaxValue;

        for (int r = 0; r < AllRotations.Length; r++)
        {
            var rotated = RotateFingerprint(candidate, r);
            var score = target.WeightedDeltaE(rotated);

            if (score < bestScore)
            {
                bestScore = score;
                bestRotation = AllRotations[r];
            }
        }

        return bestRotation;
    }

    private static ColorFingerprint RotateFingerprint(ColorFingerprint fp, int rotationIndex)
    {
        if (rotationIndex == 0) return fp;

        var map = RotationMaps[rotationIndex];
        var rotated = new QuadrantLab[fp.Regions.Length];
        for (int i = 0; i < fp.Regions.Length; i++)
            rotated[i] = fp.Regions[map[i]];

        return new ColorFingerprint(fp.Total, rotated);
    }
}
