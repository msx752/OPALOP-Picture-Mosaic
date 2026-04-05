namespace Opalop.Mosaic.Engine.Matching;

using Opalop.Domain.ValueObjects;

public enum RotationAngle { None = 0, Rotate90 = 90, Rotate180 = 180, Rotate270 = 270 }

public static class SmartRotator
{
    private static readonly RotationAngle[] AllRotations =
        [RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270];

    /// <summary>
    /// Evaluates all 4 rotation angles and picks the one with the lowest total
    /// WeightedDeltaE. This guarantees rotation never degrades the match quality.
    /// </summary>
    public static RotationAngle DetermineRotation(ColorFingerprint target, ColorFingerprint candidate)
    {
        var bestRotation = RotationAngle.None;
        float bestScore = float.MaxValue;

        foreach (var rotation in AllRotations)
        {
            var rotated = RotateFingerprint(candidate, rotation);
            var score = target.WeightedDeltaE(rotated);

            if (score < bestScore)
            {
                bestScore = score;
                bestRotation = rotation;
            }
        }

        return bestRotation;
    }

    /// <summary>
    /// Returns a new fingerprint with quadrants permuted to match the given rotation.
    /// </summary>
    private static ColorFingerprint RotateFingerprint(ColorFingerprint fp, RotationAngle rotation) => rotation switch
    {
        // TL TR     BR BL (90° CW)     BR BL (180°)     TR TL (270° CW)
        // BL BR  →  TR TL              TR TL          →  BL BR
        RotationAngle.None => fp,
        RotationAngle.Rotate90 => new ColorFingerprint(fp.Total, fp.BottomLeft, fp.TopLeft, fp.BottomRight, fp.TopRight),
        RotationAngle.Rotate180 => new ColorFingerprint(fp.Total, fp.BottomRight, fp.BottomLeft, fp.TopRight, fp.TopLeft),
        RotationAngle.Rotate270 => new ColorFingerprint(fp.Total, fp.TopRight, fp.BottomRight, fp.TopLeft, fp.BottomLeft),
        _ => fp
    };
}
