namespace Opalop.Domain.ValueObjects;

public readonly record struct QuadrantLab(float L, float A, float B)
{
    public float DeltaE(QuadrantLab other)
    {
        var dL = L - other.L;
        var dA = A - other.A;
        var dB = B - other.B;
        return MathF.Sqrt(dL * dL + dA * dA + dB * dB);
    }
}
