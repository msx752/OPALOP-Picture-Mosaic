# Color Matching Engine

## Overview

The color matching engine is the core algorithm that determines which mini photo best represents each tile of the source image. It uses CIE L\*a\*b\* color space for perceptually accurate matching.

## Pipeline

```
Source Image
  |
  v
Gaussian Blur (sigma=5) -----> Reduces noise for stable color sampling
  |
  v
Tile Grid (pxFormat) --------> Splits into N x M tiles
  |
  v
Per Tile:
  |
  +-- QuadrantAnalyzer ------> 4 quadrants + total average
  |     |
  |     +-- RGB pixel sampling per quadrant
  |     +-- LabConverter.RgbToLab() per quadrant
  |     +-- Returns ColorFingerprint (5 x QuadrantLab)
  |
  +-- Redis ZRANGEBYSCORE ----> Pre-filter by L* (lightness)
  |     |
  |     +-- Expanding tolerance: 10, 25, 50
  |
  +-- Weighted Delta-E ------> Rank candidates
  |     |
  |     +-- 0.40 * DeltaE(total)
  |     +-- 0.15 * DeltaE(top-left)
  |     +-- 0.15 * DeltaE(top-right)
  |     +-- 0.15 * DeltaE(bottom-left)
  |     +-- 0.15 * DeltaE(bottom-right)
  |
  +-- Usage Counter ----------> Prevent photo over-repetition
  |     |
  |     +-- HINCRBY job:{id}:usage {photoId} 1
  |     +-- Skip if usage >= maxPerPhoto (default 5)
  |
  +-- SmartRotator ------------> Align photo orientation
  |     |
  |     +-- Find target's most distinctive quadrant
  |     +-- Match to candidate's closest quadrant
  |     +-- Rotation: 0, 90, 180, or 270 degrees
  |
  +-- TileCompositor ----------> Draw onto canvas
        |
        +-- SkiaSharp RotateDegrees
        +-- Alpha compositing (opacity: 180/255)
```

## CIE L\*a\*b\* Color Space

### Why L\*a\*b\*?

RGB is device-dependent and perceptually non-uniform. A fixed distance in RGB does not correspond to a fixed perceived color difference.

L\*a\*b\* is designed to be perceptually uniform:
- **L\*** (0-100): lightness, black to white
- **a\*** (-128 to +127): green to red axis
- **b\*** (-128 to +127): blue to yellow axis

### Conversion: RGB -> L\*a\*b\*

```
sRGB -> Linear RGB (gamma decode)
     -> XYZ (D65 illuminant, sRGB matrix)
     -> L*a*b* (CIE standard transform)
```

Implemented in `LabConverter.RgbToLab()` with full round-trip support.

### Delta-E (Color Distance)

```
DeltaE = sqrt((L1-L2)^2 + (a1-a2)^2 + (b1-b2)^2)
```

Interpretation:
| Delta-E | Perception |
|---------|-----------|
| < 1 | Not perceptible |
| 1-2 | Close observation only |
| 2-10 | Noticeable at a glance |
| 10-50 | Colors are different |
| > 50 | Colors are unrelated |

## Color Fingerprint

Each photo (mini tile or source tile) gets a fingerprint: 5 L\*a\*b\* values.

```
+--------+--------+
| Q0(TL) | Q1(TR) |    4 quadrants + 1 total average
+--------+--------+    = 15 float values (5 x L,a,b)
| Q2(BL) | Q3(BR) |
+--------+--------+
     Total Avg
```

This captures spatial color distribution, not just a flat average. A sunset photo (bright top, dark bottom) gets a different fingerprint than a uniform orange photo.

## Redis Data Model

```
user:{uid}:colors           Sorted Set (score=L*, member=photoId)
user:{uid}:photo:{photoId}  Hash (15 fields: total_L/a/b, q0_L/a/b, ..., q3_L/a/b)
job:{jobId}:usage           Hash (field=photoId, value=usage count)
```

### Matching Flow

1. `ZRANGEBYSCORE user:colors (targetL-tolerance) (targetL+tolerance)` -> candidate list
2. For each candidate: `HGETALL user:photo:{id}` -> quadrant LAB values
3. Compute weighted Delta-E score
4. Check usage counter; skip if over limit
5. Best match wins; increment usage counter

Tolerance expands if no match found: 10 -> 25 -> 50. If still no match, tile is filled with its own average color as fallback.

## SmartRotate

After matching, the candidate photo is rotated to best align with the target tile's quadrant layout:

1. Find target's most **distinctive** quadrant (highest Delta-E from total average)
2. Find candidate's quadrant closest to that distinctive quadrant
3. Compute rotation needed to align them:

| Candidate quadrant | Target quadrant | Rotation |
|-------------------|----------------|----------|
| Same position | Same position | 0 (None) |
| TopLeft -> TopRight | | 90 CW |
| TopLeft -> BottomRight | | 180 |
| TopLeft -> BottomLeft | | 270 CW |

This ensures the brightest/most distinctive part of the photo aligns with the corresponding area of the target, improving visual coherence.

## Legacy Comparison

| Aspect | Legacy (packed RGB) | New (CIE L\*a\*b\*) |
|--------|--------------------|--------------------|
| Color space | RGB packed integer | CIE L\*a\*b\* |
| Channel weighting | R: 65536x, G: 256x, B: 1x | Equal (perceptual) |
| Comparison | 1D range on packed int | 3D Euclidean distance |
| Quadrant usage | Bug: Q3 excluded from average | All 4 quadrants correct |
| Pre-filtering | Linear scan (O(N)) | Sorted Set (O(log N)) |
| Reuse prevention | None | Per-job usage counter |
| Index computation | Every request (disk I/O) | Once at upload (Redis) |
