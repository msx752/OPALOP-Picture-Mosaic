namespace Opalop.Domain.ValueObjects;

public readonly record struct PixFormat
{
    public int Size { get; }
    private static readonly int[] ValidSizes = [12, 20, 36, 48, 64, 94];
    private PixFormat(int size) => Size = size;

    public static PixFormat From(int size)
    {
        if (!ValidSizes.Contains(size))
            throw new ArgumentException($"Invalid pixel format: {size}. Valid sizes: {string.Join(", ", ValidSizes)}");
        return new PixFormat(size);
    }

    public static PixFormat Default => new(94);
}
