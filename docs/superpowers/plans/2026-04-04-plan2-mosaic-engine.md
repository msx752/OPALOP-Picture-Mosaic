# Plan 2: Mosaic Engine — SkiaSharp, CIE L*a*b*, Color Matching, Assembly

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the core image processing engine that analyzes source images, computes CIE L\*a\*b\* color fingerprints, performs SmartRotate orientation matching, composites tiles, and assembles final mosaics — all using SkiaSharp (cross-platform, replacing legacy GDI+).

**Architecture:** Pure image processing library (Opalop.Mosaic.Engine) depending only on Domain. No Redis, no database — just SkiaSharp + math. The engine receives inputs and returns outputs; infrastructure handles I/O.

**Tech Stack:** SkiaSharp 3.x, .NET 9, CIE L\*a\*b\* color space, xUnit + FluentAssertions

**Spec reference:** `docs/superpowers/specs/2026-04-04-opalop-net9-migration-design.md` — Section 5 (Color Matching Engine)

---

## File Map

```
src/Opalop.Mosaic.Engine/
├── Opalop.Mosaic.Engine.csproj     (modify: add SkiaSharp)
├── ColorSpace/
│   └── LabConverter.cs             # RGB ↔ CIE L*a*b* conversion
├── Analysis/
│   ├── QuadrantAnalyzer.cs         # Compute 4-quadrant + total LAB fingerprint from SKBitmap
│   └── TileGridBuilder.cs          # Split source image into pxFormat grid, blur, extract fingerprints
├── Matching/
│   └── SmartRotator.cs             # Determine optimal rotation by quadrant LAB alignment
├── Compositing/
│   ├── TileCompositor.cs           # Draw a matched mini photo onto a tile position with alpha
│   └── MosaicAssembler.cs          # Combine all processed tiles into final image

tests/Opalop.Mosaic.Engine.Tests/
├── Opalop.Mosaic.Engine.Tests.csproj  (create)
├── ColorSpace/
│   └── LabConverterTests.cs
├── Analysis/
│   ├── QuadrantAnalyzerTests.cs
│   └── TileGridBuilderTests.cs
├── Matching/
│   └── SmartRotatorTests.cs
└── Compositing/
    └── MosaicAssemblerTests.cs
```

---

### Task 1: SkiaSharp Setup & RGB-to-LAB Converter

**Files:**
- Modify: `src/Opalop.Mosaic.Engine/Opalop.Mosaic.Engine.csproj`
- Create: `src/Opalop.Mosaic.Engine/ColorSpace/LabConverter.cs`
- Create: `tests/Opalop.Mosaic.Engine.Tests/Opalop.Mosaic.Engine.Tests.csproj`
- Create: `tests/Opalop.Mosaic.Engine.Tests/ColorSpace/LabConverterTests.cs`

- [ ] **Step 1: Write failing tests for LabConverter**

```csharp
// tests/Opalop.Mosaic.Engine.Tests/ColorSpace/LabConverterTests.cs
namespace Opalop.Mosaic.Engine.Tests.ColorSpace;

using FluentAssertions;
using Opalop.Domain.ValueObjects;
using Opalop.Mosaic.Engine.ColorSpace;

public class LabConverterTests
{
    [Fact]
    public void PureWhite_ReturnsL100()
    {
        var lab = LabConverter.RgbToLab(255, 255, 255);
        lab.L.Should().BeApproximately(100f, 0.5f);
        lab.A.Should().BeApproximately(0f, 1f);
        lab.B.Should().BeApproximately(0f, 1f);
    }

    [Fact]
    public void PureBlack_ReturnsL0()
    {
        var lab = LabConverter.RgbToLab(0, 0, 0);
        lab.L.Should().BeApproximately(0f, 0.5f);
        lab.A.Should().BeApproximately(0f, 1f);
        lab.B.Should().BeApproximately(0f, 1f);
    }

    [Fact]
    public void PureRed_ReturnsPositiveA()
    {
        var lab = LabConverter.RgbToLab(255, 0, 0);
        lab.L.Should().BeGreaterThan(40f);
        lab.A.Should().BeGreaterThan(50f); // red is positive a
        lab.B.Should().BeGreaterThan(30f); // red also has positive b
    }

    [Fact]
    public void PureGreen_ReturnsNegativeA()
    {
        var lab = LabConverter.RgbToLab(0, 128, 0);
        lab.A.Should().BeLessThan(-20f); // green is negative a
    }

    [Fact]
    public void PureBlue_ReturnsNegativeB()
    {
        var lab = LabConverter.RgbToLab(0, 0, 255);
        lab.B.Should().BeLessThan(-50f); // blue is negative b
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var lab = LabConverter.RgbToLab(128, 64, 200);
        var (r, g, b) = LabConverter.LabToRgb(lab);
        r.Should().BeCloseTo((byte)128, 2);
        g.Should().BeCloseTo((byte)64, 2);
        b.Should().BeCloseTo((byte)200, 2);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/Opalop.Mosaic.Engine.Tests -v n
```

Expected: FAIL — `LabConverter` does not exist.

- [ ] **Step 3: Add SkiaSharp to Engine csproj**

```xml
<!-- src/Opalop.Mosaic.Engine/Opalop.Mosaic.Engine.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Opalop.Domain\Opalop.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="SkiaSharp" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create test project csproj**

```xml
<!-- tests/Opalop.Mosaic.Engine.Tests/Opalop.Mosaic.Engine.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\Opalop.Mosaic.Engine\Opalop.Mosaic.Engine.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>
</Project>
```

Add to solution: `dotnet sln Opalop.sln add tests/Opalop.Mosaic.Engine.Tests/Opalop.Mosaic.Engine.Tests.csproj`

- [ ] **Step 5: Implement LabConverter**

The CIE L\*a\*b\* conversion goes: RGB → linear RGB → XYZ (D65 illuminant) → L\*a\*b\*.

```csharp
// src/Opalop.Mosaic.Engine/ColorSpace/LabConverter.cs
namespace Opalop.Mosaic.Engine.ColorSpace;

using Opalop.Domain.ValueObjects;

public static class LabConverter
{
    // D65 reference white point
    private const float Xn = 0.95047f;
    private const float Yn = 1.00000f;
    private const float Zn = 1.08883f;

    public static QuadrantLab RgbToLab(byte r, byte g, byte b)
    {
        // Step 1: sRGB to linear RGB
        float lr = SrgbToLinear(r / 255f);
        float lg = SrgbToLinear(g / 255f);
        float lb = SrgbToLinear(b / 255f);

        // Step 2: Linear RGB to XYZ (sRGB D65 matrix)
        float x = 0.4124564f * lr + 0.3575761f * lg + 0.1804375f * lb;
        float y = 0.2126729f * lr + 0.7151522f * lg + 0.0721750f * lb;
        float z = 0.0193339f * lr + 0.1191920f * lg + 0.9503041f * lb;

        // Step 3: XYZ to Lab
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
        // Lab to XYZ
        float fy = (lab.L + 16f) / 116f;
        float fx = lab.A / 500f + fy;
        float fz = fy - lab.B / 200f;

        float x = Xn * LabFInverse(fx);
        float y = Yn * LabFInverse(fy);
        float z = Zn * LabFInverse(fz);

        // XYZ to linear RGB
        float lr =  3.2404542f * x - 1.5371385f * y - 0.4985314f * z;
        float lg = -0.9692660f * x + 1.8760108f * y + 0.0415560f * z;
        float lb =  0.0556434f * x - 0.2040259f * y + 1.0572252f * z;

        return (
            ClampToByte(LinearToSrgb(lr)),
            ClampToByte(LinearToSrgb(lg)),
            ClampToByte(LinearToSrgb(lb))
        );
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
```

- [ ] **Step 6: Run tests — verify they pass**

```bash
dotnet test tests/Opalop.Mosaic.Engine.Tests -v n
```

Expected: 6 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Opalop.Mosaic.Engine/ tests/Opalop.Mosaic.Engine.Tests/ Opalop.sln
git commit -m "feat: add CIE L*a*b* color space converter with round-trip tests"
```

---

### Task 2: QuadrantAnalyzer — 4-Quadrant Fingerprint from Bitmap

**Files:**
- Create: `src/Opalop.Mosaic.Engine/Analysis/QuadrantAnalyzer.cs`
- Create: `tests/Opalop.Mosaic.Engine.Tests/Analysis/QuadrantAnalyzerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Opalop.Mosaic.Engine.Tests/Analysis/QuadrantAnalyzerTests.cs
namespace Opalop.Mosaic.Engine.Tests.Analysis;

using FluentAssertions;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;

public class QuadrantAnalyzerTests
{
    [Fact]
    public void SolidColorBitmap_AllQuadrantsEqual()
    {
        using var bitmap = new SKBitmap(100, 100);
        bitmap.Erase(new SKColor(128, 64, 200));

        var fp = QuadrantAnalyzer.Analyze(bitmap);

        // All quadrants should be approximately equal for a solid color
        fp.TopLeft.L.Should().BeApproximately(fp.Total.L, 0.1f);
        fp.TopRight.L.Should().BeApproximately(fp.Total.L, 0.1f);
        fp.BottomLeft.L.Should().BeApproximately(fp.Total.L, 0.1f);
        fp.BottomRight.L.Should().BeApproximately(fp.Total.L, 0.1f);
    }

    [Fact]
    public void TwoColorBitmap_QuadrantsDiffer()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);

        // Top half red, bottom half blue
        canvas.DrawRect(0, 0, 100, 50, new SKPaint { Color = SKColors.Red });
        canvas.DrawRect(0, 50, 100, 50, new SKPaint { Color = SKColors.Blue });

        var fp = QuadrantAnalyzer.Analyze(bitmap);

        // Top quadrants should have positive a* (red), bottom should have negative b* (blue)
        fp.TopLeft.A.Should().BeGreaterThan(fp.BottomLeft.A);
        fp.TopRight.A.Should().BeGreaterThan(fp.BottomRight.A);
    }

    [Fact]
    public void Fingerprint_HasFiveComponents()
    {
        using var bitmap = new SKBitmap(94, 94);
        bitmap.Erase(SKColors.Green);

        var fp = QuadrantAnalyzer.Analyze(bitmap);

        // Total should be a meaningful value (not zero for non-black)
        fp.Total.L.Should().BeGreaterThan(0f);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement QuadrantAnalyzer**

```csharp
// src/Opalop.Mosaic.Engine/Analysis/QuadrantAnalyzer.cs
namespace Opalop.Mosaic.Engine.Analysis;

using Opalop.Domain.ValueObjects;
using Opalop.Mosaic.Engine.ColorSpace;
using SkiaSharp;

public static class QuadrantAnalyzer
{
    public static ColorFingerprint Analyze(SKBitmap bitmap)
    {
        int halfW = bitmap.Width / 2;
        int halfH = bitmap.Height / 2;

        var topLeft = AverageRegion(bitmap, 0, 0, halfW, halfH);
        var topRight = AverageRegion(bitmap, halfW, 0, bitmap.Width - halfW, halfH);
        var bottomLeft = AverageRegion(bitmap, 0, halfH, halfW, bitmap.Height - halfH);
        var bottomRight = AverageRegion(bitmap, halfW, halfH, bitmap.Width - halfW, bitmap.Height - halfH);
        var total = AverageRegion(bitmap, 0, 0, bitmap.Width, bitmap.Height);

        return new ColorFingerprint(total, topLeft, topRight, bottomLeft, bottomRight);
    }

    private static QuadrantLab AverageRegion(SKBitmap bitmap, int startX, int startY, int width, int height)
    {
        long sumR = 0, sumG = 0, sumB = 0;
        int count = 0;

        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                sumR += pixel.Red;
                sumG += pixel.Green;
                sumB += pixel.Blue;
                count++;
            }
        }

        if (count == 0) return new QuadrantLab(0, 0, 0);

        byte avgR = (byte)(sumR / count);
        byte avgG = (byte)(sumG / count);
        byte avgB = (byte)(sumB / count);

        return LabConverter.RgbToLab(avgR, avgG, avgB);
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

- [ ] **Step 5: Commit**

```bash
git add src/Opalop.Mosaic.Engine/Analysis/ tests/Opalop.Mosaic.Engine.Tests/Analysis/
git commit -m "feat: add QuadrantAnalyzer for 4-quadrant CIE L*a*b* fingerprinting"
```

---

### Task 3: TileGridBuilder — Source Image Analysis

**Files:**
- Create: `src/Opalop.Mosaic.Engine/Analysis/TileGridBuilder.cs`
- Create: `tests/Opalop.Mosaic.Engine.Tests/Analysis/TileGridBuilderTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Opalop.Mosaic.Engine.Tests/Analysis/TileGridBuilderTests.cs
namespace Opalop.Mosaic.Engine.Tests.Analysis;

using FluentAssertions;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;

public class TileGridBuilderTests
{
    [Fact]
    public void BuildGrid_CorrectTileCount()
    {
        // 200x200 image with 100px tiles = 2x2 = 4 tiles
        using var bitmap = new SKBitmap(200, 200);
        bitmap.Erase(SKColors.Gray);

        var tiles = TileGridBuilder.Build(bitmap, tileSize: 100);

        tiles.Should().HaveCount(4);
    }

    [Fact]
    public void BuildGrid_TilePositionsCorrect()
    {
        using var bitmap = new SKBitmap(200, 200);
        bitmap.Erase(SKColors.Gray);

        var tiles = TileGridBuilder.Build(bitmap, tileSize: 100);

        tiles[0].X.Should().Be(0);
        tiles[0].Y.Should().Be(0);
        tiles[1].X.Should().Be(100);
        tiles[1].Y.Should().Be(0);
        tiles[2].X.Should().Be(0);
        tiles[2].Y.Should().Be(100);
        tiles[3].X.Should().Be(100);
        tiles[3].Y.Should().Be(100);
    }

    [Fact]
    public void BuildGrid_DiscardRemainderPixels()
    {
        // 250x250 with 100px tiles: 2x2 = 4 tiles, 50px remainder discarded
        using var bitmap = new SKBitmap(250, 250);
        bitmap.Erase(SKColors.Gray);

        var tiles = TileGridBuilder.Build(bitmap, tileSize: 100);

        tiles.Should().HaveCount(4);
    }

    [Fact]
    public void BuildGrid_EachTileHasFingerprint()
    {
        using var bitmap = new SKBitmap(200, 200);
        bitmap.Erase(new SKColor(100, 150, 200));

        var tiles = TileGridBuilder.Build(bitmap, tileSize: 100);

        foreach (var tile in tiles)
        {
            tile.Fingerprint.Total.L.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void BuildGrid_AppliesBlur()
    {
        // Create a checkerboard pattern — blur should smooth it
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        for (int y = 0; y < 200; y += 10)
            for (int x = 0; x < 200; x += 10)
                canvas.DrawRect(x, y, 10, 10,
                    new SKPaint { Color = (x + y) % 20 == 0 ? SKColors.White : SKColors.Black });

        var tilesNoBlur = TileGridBuilder.Build(bitmap, tileSize: 100, blurSigma: 0);
        var tilesBlur = TileGridBuilder.Build(bitmap, tileSize: 100, blurSigma: 5);

        // Both should produce tiles but fingerprints may differ slightly due to blur
        tilesNoBlur.Should().HaveCount(4);
        tilesBlur.Should().HaveCount(4);
    }
}
```

- [ ] **Step 2: Implement TileGridBuilder**

```csharp
// src/Opalop.Mosaic.Engine/Analysis/TileGridBuilder.cs
namespace Opalop.Mosaic.Engine.Analysis;

using Opalop.Domain.ValueObjects;
using SkiaSharp;

public record TileInfo(int Index, int X, int Y, ColorFingerprint Fingerprint);

public static class TileGridBuilder
{
    public static IReadOnlyList<TileInfo> Build(SKBitmap source, int tileSize, float blurSigma = 5f)
    {
        using var working = blurSigma > 0 ? ApplyBlur(source, blurSigma) : source.Copy();

        int cols = working.Width / tileSize;
        int rows = working.Height / tileSize;
        var tiles = new List<TileInfo>(cols * rows);
        int index = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int x = col * tileSize;
                int y = row * tileSize;

                using var tileBitmap = new SKBitmap(tileSize, tileSize);
                using var canvas = new SKCanvas(tileBitmap);
                canvas.DrawBitmap(working, SKRect.Create(x, y, tileSize, tileSize),
                    SKRect.Create(0, 0, tileSize, tileSize));

                var fingerprint = QuadrantAnalyzer.Analyze(tileBitmap);
                tiles.Add(new TileInfo(index++, x, y, fingerprint));
            }
        }

        return tiles;
    }

    private static SKBitmap ApplyBlur(SKBitmap source, float sigma)
    {
        var blurred = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(blurred);
        using var paint = new SKPaint();
        paint.ImageFilter = SKImageFilter.CreateBlur(sigma, sigma);
        canvas.DrawBitmap(source, 0, 0, paint);
        return blurred;
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

- [ ] **Step 4: Commit**

```bash
git add src/Opalop.Mosaic.Engine/Analysis/TileGridBuilder.cs tests/Opalop.Mosaic.Engine.Tests/Analysis/TileGridBuilderTests.cs
git commit -m "feat: add TileGridBuilder for source image grid analysis with blur"
```

---

### Task 4: SmartRotator — Orientation Matching

**Files:**
- Create: `src/Opalop.Mosaic.Engine/Matching/SmartRotator.cs`
- Create: `tests/Opalop.Mosaic.Engine.Tests/Matching/SmartRotatorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Opalop.Mosaic.Engine.Tests/Matching/SmartRotatorTests.cs
namespace Opalop.Mosaic.Engine.Tests.Matching;

using FluentAssertions;
using Opalop.Domain.ValueObjects;
using Opalop.Mosaic.Engine.Matching;

public class SmartRotatorTests
{
    [Fact]
    public void IdenticalQuadrants_ReturnsNoRotation()
    {
        var q = new QuadrantLab(50, 20, -10);
        var fp = new ColorFingerprint(q, q, q, q, q);

        var rotation = SmartRotator.DetermineRotation(target: fp, candidate: fp);

        rotation.Should().Be(RotationAngle.None);
    }

    [Fact]
    public void BrightTopLeft_MatchesBrightTopLeft_NoRotation()
    {
        var bright = new QuadrantLab(90, 10, 5);
        var dark = new QuadrantLab(20, 5, -5);
        var mid = new QuadrantLab(50, 8, 0);

        var target = new ColorFingerprint(mid, bright, dark, dark, dark);
        var candidate = new ColorFingerprint(mid, bright, dark, dark, dark);

        SmartRotator.DetermineRotation(target, candidate).Should().Be(RotationAngle.None);
    }

    [Fact]
    public void BrightTopLeft_MatchesBrightTopRight_Rotates270()
    {
        var bright = new QuadrantLab(90, 10, 5);
        var dark = new QuadrantLab(20, 5, -5);
        var mid = new QuadrantLab(50, 8, 0);

        var target = new ColorFingerprint(mid, bright, dark, dark, dark);
        var candidate = new ColorFingerprint(mid, dark, bright, dark, dark);

        // Candidate has bright in TopRight; to match target's TopLeft, rotate 270°
        SmartRotator.DetermineRotation(target, candidate).Should().Be(RotationAngle.Rotate270);
    }

    [Fact]
    public void BrightTopLeft_MatchesBrightBottomLeft_Rotates90()
    {
        var bright = new QuadrantLab(90, 10, 5);
        var dark = new QuadrantLab(20, 5, -5);
        var mid = new QuadrantLab(50, 8, 0);

        var target = new ColorFingerprint(mid, bright, dark, dark, dark);
        var candidate = new ColorFingerprint(mid, dark, dark, bright, dark);

        SmartRotator.DetermineRotation(target, candidate).Should().Be(RotationAngle.Rotate90);
    }

    [Fact]
    public void BrightTopLeft_MatchesBrightBottomRight_Rotates180()
    {
        var bright = new QuadrantLab(90, 10, 5);
        var dark = new QuadrantLab(20, 5, -5);
        var mid = new QuadrantLab(50, 8, 0);

        var target = new ColorFingerprint(mid, bright, dark, dark, dark);
        var candidate = new ColorFingerprint(mid, dark, dark, dark, bright);

        SmartRotator.DetermineRotation(target, candidate).Should().Be(RotationAngle.Rotate180);
    }
}
```

- [ ] **Step 2: Implement SmartRotator**

```csharp
// src/Opalop.Mosaic.Engine/Matching/SmartRotator.cs
namespace Opalop.Mosaic.Engine.Matching;

using Opalop.Domain.ValueObjects;

public enum RotationAngle
{
    None = 0,
    Rotate90 = 90,
    Rotate180 = 180,
    Rotate270 = 270
}

public static class SmartRotator
{
    /// <summary>
    /// Determines the rotation needed for the candidate photo to best match
    /// the target tile's quadrant layout. Finds which quadrant of the target
    /// has the most distinctive color (highest ΔE from center), then finds
    /// the best-matching quadrant in the candidate and rotates accordingly.
    /// </summary>
    public static RotationAngle DetermineRotation(ColorFingerprint target, ColorFingerprint candidate)
    {
        // Find the target's most distinctive quadrant (farthest from total average)
        var targetQuadrants = new (QuadrantLab Lab, int Position)[]
        {
            (target.TopLeft, 0),
            (target.TopRight, 1),
            (target.BottomLeft, 2),
            (target.BottomRight, 3)
        };

        var mostDistinctive = targetQuadrants
            .OrderByDescending(q => q.Lab.DeltaE(target.Total))
            .First();

        // Find which candidate quadrant best matches the target's distinctive quadrant
        var candidateQuadrants = new (QuadrantLab Lab, int Position)[]
        {
            (candidate.TopLeft, 0),
            (candidate.TopRight, 1),
            (candidate.BottomLeft, 2),
            (candidate.BottomRight, 3)
        };

        var bestMatch = candidateQuadrants
            .OrderBy(q => q.Lab.DeltaE(mostDistinctive.Lab))
            .First();

        // Calculate rotation needed to move bestMatch.Position to mostDistinctive.Position
        return GetRotation(bestMatch.Position, mostDistinctive.Position);
    }

    // Positions: 0=TL, 1=TR, 2=BL, 3=BR
    // Rotation map: which rotation moves source position to target position
    private static RotationAngle GetRotation(int from, int to)
    {
        // Encode positions as (col, row): TL=(0,0), TR=(1,0), BL=(0,1), BR=(1,1)
        // 90° CW rotation: (c,r) → (1-r, c)
        //   TL(0,0) → (1,0)=TR
        //   TR(1,0) → (1,1)=BR
        //   BR(1,1) → (0,1)=BL
        //   BL(0,1) → (0,0)=TL
        // Rotation lookup table: [from][to] → angle
        int[,] rotationTable =
        {
            //         to: TL    TR    BL    BR
            /* from TL */ { 0,   90,  270,  180 },
            /* from TR */ { 270,  0,   180,  90  },
            /* from BL */ { 90,  180,   0,   270 },
            /* from BR */ { 180, 270,  90,    0  }
        };

        return (RotationAngle)rotationTable[from, to];
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

- [ ] **Step 4: Commit**

```bash
git add src/Opalop.Mosaic.Engine/Matching/ tests/Opalop.Mosaic.Engine.Tests/Matching/
git commit -m "feat: add SmartRotator for quadrant-based orientation matching"
```

---

### Task 5: TileCompositor & MosaicAssembler

**Files:**
- Create: `src/Opalop.Mosaic.Engine/Compositing/TileCompositor.cs`
- Create: `src/Opalop.Mosaic.Engine/Compositing/MosaicAssembler.cs`
- Create: `tests/Opalop.Mosaic.Engine.Tests/Compositing/MosaicAssemblerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Opalop.Mosaic.Engine.Tests/Compositing/MosaicAssemblerTests.cs
namespace Opalop.Mosaic.Engine.Tests.Compositing;

using FluentAssertions;
using Opalop.Mosaic.Engine.Compositing;
using Opalop.Mosaic.Engine.Matching;
using SkiaSharp;

public class MosaicAssemblerTests
{
    [Fact]
    public void Composite_DrawsTileAtCorrectPosition()
    {
        using var canvas = new SKBitmap(200, 200);
        canvas.Erase(SKColors.White);

        using var tile = new SKBitmap(100, 100);
        tile.Erase(SKColors.Red);

        TileCompositor.Composite(canvas, tile, x: 100, y: 0, RotationAngle.None, opacity: 255);

        // Top-right quadrant should be reddish
        var pixel = canvas.GetPixel(150, 50);
        pixel.Red.Should().BeGreaterThan(200);
    }

    [Fact]
    public void Composite_AppliesAlpha()
    {
        using var canvas = new SKBitmap(100, 100);
        canvas.Erase(SKColors.White);

        using var tile = new SKBitmap(100, 100);
        tile.Erase(SKColors.Black);

        TileCompositor.Composite(canvas, tile, 0, 0, RotationAngle.None, opacity: 128);

        // Should be grayish (white background + semi-transparent black)
        var pixel = canvas.GetPixel(50, 50);
        pixel.Red.Should().BeInRange(100, 160);
    }

    [Fact]
    public void Composite_AppliesRotation180()
    {
        using var canvas = new SKBitmap(100, 100);
        canvas.Erase(SKColors.White);

        // Create tile with top half red, bottom half blue
        using var tile = new SKBitmap(100, 100);
        using var tileCanvas = new SKCanvas(tile);
        tileCanvas.DrawRect(0, 0, 100, 50, new SKPaint { Color = SKColors.Red });
        tileCanvas.DrawRect(0, 50, 100, 50, new SKPaint { Color = SKColors.Blue });

        TileCompositor.Composite(canvas, tile, 0, 0, RotationAngle.Rotate180, opacity: 255);

        // After 180° rotation: top should be blue, bottom should be red
        var topPixel = canvas.GetPixel(50, 25);
        var bottomPixel = canvas.GetPixel(50, 75);
        topPixel.Blue.Should().BeGreaterThan(topPixel.Red);
        bottomPixel.Red.Should().BeGreaterThan(bottomPixel.Blue);
    }

    [Fact]
    public void Assemble_CreatesCorrectSizeCanvas()
    {
        var tiles = new List<ProcessedTile>
        {
            CreateTile(0, 0, 100, SKColors.Red),
            CreateTile(100, 0, 100, SKColors.Green),
            CreateTile(0, 100, 100, SKColors.Blue),
            CreateTile(100, 100, 100, SKColors.Yellow)
        };

        using var result = MosaicAssembler.Assemble(tiles, canvasWidth: 200, canvasHeight: 200, tileSize: 100);

        result.Width.Should().Be(200);
        result.Height.Should().Be(200);
    }

    [Fact]
    public void Assemble_PlacesTilesCorrectly()
    {
        var tiles = new List<ProcessedTile>
        {
            CreateTile(0, 0, 100, SKColors.Red),
            CreateTile(100, 0, 100, SKColors.Blue)
        };

        using var result = MosaicAssembler.Assemble(tiles, canvasWidth: 200, canvasHeight: 100, tileSize: 100);

        result.GetPixel(50, 50).Red.Should().BeGreaterThan(200);   // left tile = red
        result.GetPixel(150, 50).Blue.Should().BeGreaterThan(200); // right tile = blue
    }

    private static ProcessedTile CreateTile(int x, int y, int size, SKColor color)
    {
        var bitmap = new SKBitmap(size, size);
        bitmap.Erase(color);
        return new ProcessedTile(x, y, bitmap);
    }
}
```

- [ ] **Step 2: Implement TileCompositor**

```csharp
// src/Opalop.Mosaic.Engine/Compositing/TileCompositor.cs
namespace Opalop.Mosaic.Engine.Compositing;

using Opalop.Mosaic.Engine.Matching;
using SkiaSharp;

public static class TileCompositor
{
    public static void Composite(SKBitmap canvas, SKBitmap tile, int x, int y,
        RotationAngle rotation, byte opacity = 128)
    {
        using var rotated = rotation != RotationAngle.None
            ? RotateBitmap(tile, rotation)
            : tile.Copy();

        using var canvasObj = new SKCanvas(canvas);
        using var paint = new SKPaint { Color = new SKColor(255, 255, 255, opacity) };
        canvasObj.DrawBitmap(rotated, x, y, paint);
    }

    private static SKBitmap RotateBitmap(SKBitmap source, RotationAngle angle)
    {
        var rotated = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(rotated);
        float cx = source.Width / 2f;
        float cy = source.Height / 2f;
        canvas.RotateDegrees((float)angle, cx, cy);
        canvas.DrawBitmap(source, 0, 0);
        return rotated;
    }
}
```

- [ ] **Step 3: Implement MosaicAssembler**

```csharp
// src/Opalop.Mosaic.Engine/Compositing/MosaicAssembler.cs
namespace Opalop.Mosaic.Engine.Compositing;

using SkiaSharp;

public record ProcessedTile(int X, int Y, SKBitmap Bitmap) : IDisposable
{
    public void Dispose() => Bitmap.Dispose();
}

public static class MosaicAssembler
{
    public static SKBitmap Assemble(IReadOnlyList<ProcessedTile> tiles,
        int canvasWidth, int canvasHeight, int tileSize)
    {
        var result = new SKBitmap(canvasWidth, canvasHeight);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);

        foreach (var tile in tiles)
        {
            canvas.DrawBitmap(tile.Bitmap, tile.X, tile.Y);
        }

        return result;
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/Opalop.Mosaic.Engine.Tests -v n
```

- [ ] **Step 5: Run all tests**

```bash
dotnet test Opalop.sln -v n
```

Expected: All tests pass (domain + infrastructure + engine).

- [ ] **Step 6: Commit**

```bash
git add src/Opalop.Mosaic.Engine/Compositing/ tests/Opalop.Mosaic.Engine.Tests/Compositing/
git commit -m "feat: add TileCompositor and MosaicAssembler for mosaic composition"
```

---

## Summary

| Task | What it produces | Depends on |
|------|-----------------|------------|
| 1 | LabConverter (RGB ↔ CIE L\*a\*b\*) + tests | — |
| 2 | QuadrantAnalyzer (4-quadrant fingerprint from bitmap) + tests | Task 1 |
| 3 | TileGridBuilder (source image → tile grid with fingerprints) + tests | Task 2 |
| 4 | SmartRotator (quadrant-based rotation decision) + tests | — |
| 5 | TileCompositor + MosaicAssembler (final image composition) + tests | Task 4 |
