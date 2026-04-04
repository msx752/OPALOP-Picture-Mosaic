namespace Opalop.Mosaic.Engine.ColorSpace;

using Opalop.Domain.ValueObjects;

public static class LabConverter
{
    private const float Xn = 0.95047f;
    private const float Yn = 1.00000f;
    private const float Zn = 1.08883f;

    public static QuadrantLab RgbToLab(byte r, byte g, byte b)
    {
        float lr = SrgbToLinear(r / 255f);
        float lg = SrgbToLinear(g / 255f);
        float lb = SrgbToLinear(b / 255f);

        float x = 0.4124564f * lr + 0.3575761f * lg + 0.1804375f * lb;
        float y = 0.2126729f * lr + 0.7151522f * lg + 0.0721750f * lb;
        float z = 0.0193339f * lr + 0.1191920f * lg + 0.9503041f * lb;

        float fx = LabF(x / Xn);
        float fy = LabF(y / Yn);
        float fz = LabF(z / Zn);

        float L = 116f * fy - 16f;
        float a = 500f * (fx - fy);
        float bVal = 200f * (fy - fz);

        return new QuadrantLab(L, a, bVal);
    }

    public static (byte R, byte G, byte B) LabToRgb(QuadrantLab lab)
    {
        float fy = (lab.L + 16f) / 116f;
        float fx = lab.A / 500f + fy;
        float fz = fy - lab.B / 200f;

        float x = Xn * LabFInverse(fx);
        float y = Yn * LabFInverse(fy);
        float z = Zn * LabFInverse(fz);

        float lr =  3.2404542f * x - 1.5371385f * y - 0.4985314f * z;
        float lg = -0.9692660f * x + 1.8760108f * y + 0.0415560f * z;
        float lb =  0.0556434f * x - 0.2040259f * y + 1.0572252f * z;

        return (ClampToByte(LinearToSrgb(lr)), ClampToByte(LinearToSrgb(lg)), ClampToByte(LinearToSrgb(lb)));
    }

    private static float SrgbToLinear(float c)
        => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    private static float LinearToSrgb(float c)
        => c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

    private static float LabF(float t)
        => t > 0.008856f ? MathF.Cbrt(t) : 7.787f * t + 16f / 116f;

    private static float LabFInverse(float t)
        => t > 0.206893f ? t * t * t : (t - 16f / 116f) / 7.787f;

    private static byte ClampToByte(float v)
        => (byte)Math.Clamp((int)(v * 255f + 0.5f), 0, 255);
}
