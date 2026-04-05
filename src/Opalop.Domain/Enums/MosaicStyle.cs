namespace Opalop.Domain.Enums;

public enum MosaicStyle
{
    /// <summary>
    /// Tiles drawn semi-transparent on top of the original source image.
    /// Original photo visible underneath. Legacy OPALOP style.
    /// </summary>
    Overlay = 0,

    /// <summary>
    /// Tiles drawn fully opaque. Original image emerges purely from
    /// color matching. Classic photomosaic style (AndreaMosaic-like).
    /// </summary>
    Classic = 1
}
