namespace Opalop.Domain.ValueObjects;

public readonly record struct QuadrantLab(float L, float A, float B)
{
    /// <summary>CIE76 Delta-E — fast Euclidean distance for pre-filtering.</summary>
    public float DeltaE(QuadrantLab other)
    {
        var dL = L - other.L;
        var dA = A - other.A;
        var dB = B - other.B;
        return MathF.Sqrt(dL * dL + dA * dA + dB * dB);
    }

    /// <summary>
    /// CIEDE2000 — perceptually uniform color difference (ISO/CIE 11664-6:2014).
    /// ~30% more accurate than CIE76 for blues and grays.
    /// </summary>
    public float DeltaE2000(QuadrantLab other)
    {
        float lBar = (L + other.L) / 2f;
        float c1 = MathF.Sqrt(A * A + B * B);
        float c2 = MathF.Sqrt(other.A * other.A + other.B * other.B);
        float cBar = (c1 + c2) / 2f;

        float cBar7 = MathF.Pow(cBar, 7);
        float g = 0.5f * (1 - MathF.Sqrt(cBar7 / (cBar7 + 6103515625f))); // 25^7

        float a1P = A * (1 + g);
        float a2P = other.A * (1 + g);

        float c1P = MathF.Sqrt(a1P * a1P + B * B);
        float c2P = MathF.Sqrt(a2P * a2P + other.B * other.B);
        float cBarP = (c1P + c2P) / 2f;

        float h1P = MathF.Atan2(B, a1P);
        if (h1P < 0) h1P += 2 * MathF.PI;
        float h2P = MathF.Atan2(other.B, a2P);
        if (h2P < 0) h2P += 2 * MathF.PI;

        float hBarP;
        float dhP;
        if (MathF.Abs(h1P - h2P) <= MathF.PI)
        {
            hBarP = (h1P + h2P) / 2f;
            dhP = h2P - h1P;
        }
        else if (h2P <= h1P)
        {
            hBarP = (h1P + h2P + 2 * MathF.PI) / 2f;
            dhP = h2P - h1P + 2 * MathF.PI;
        }
        else
        {
            hBarP = (h1P + h2P - 2 * MathF.PI) / 2f;
            dhP = h2P - h1P - 2 * MathF.PI;
        }
        if (c1P == 0 || c2P == 0) { dhP = 0; hBarP = h1P + h2P; }

        float dLP = other.L - L;
        float dCP = c2P - c1P;
        float dHP = 2 * MathF.Sqrt(c1P * c2P) * MathF.Sin(dhP / 2f);

        float t = 1
            - 0.17f * MathF.Cos(hBarP - 0.5236f)
            + 0.24f * MathF.Cos(2 * hBarP)
            + 0.32f * MathF.Cos(3 * hBarP + 0.1047f)
            - 0.20f * MathF.Cos(4 * hBarP - 1.0996f);

        float lBar50sq = (lBar - 50) * (lBar - 50);
        float sL = 1 + 0.015f * lBar50sq / MathF.Sqrt(20 + lBar50sq);
        float sC = 1 + 0.045f * cBarP;
        float sH = 1 + 0.015f * cBarP * t;

        float cBarP7 = MathF.Pow(cBarP, 7);
        float rT = -2 * MathF.Sqrt(cBarP7 / (cBarP7 + 6103515625f))
                   * MathF.Sin(1.0472f * MathF.Exp(-((hBarP * 180f / MathF.PI - 275f) / 25f * ((hBarP * 180f / MathF.PI - 275f) / 25f))));

        float valL = dLP / sL;
        float valC = dCP / sC;
        float valH = dHP / sH;

        return MathF.Sqrt(valL * valL + valC * valC + valH * valH + rT * valC * valH);
    }
}
