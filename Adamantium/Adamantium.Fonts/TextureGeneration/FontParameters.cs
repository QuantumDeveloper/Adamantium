using System;

namespace Adamantium.Fonts.TextureGeneration;

public class FontParameters
{
    public FontParameters(
        uint msdfTextureSize, 
        byte sampleRate, 
        byte pixelRange, 
        uint startGlyphIndex,
        uint glyphCount,
        GlyphSortingVariant sortingVariant,
        GlyphPlacingVariant placingVariant,
        uint glyphMargin)
    {
        MsdfTextureSize = msdfTextureSize;
        SampleRate = sampleRate;
        PixelRange = pixelRange;
        StartGlyphIndex = startGlyphIndex;
        GlyphCount = glyphCount;
        SortingVariant = sortingVariant;
        PlacingVariant = placingVariant;
        // Never let the margin drop below the field's outside ramp (pxRange/2 texels): a smaller margin
        // lets the distance field bleed into the neighbouring atlas cell under bilinear sampling. Callers
        // may pass a larger margin for extra padding, but not a smaller (unsafe) one.
        GlyphMargin = Math.Max(glyphMargin, (uint)Math.Ceiling(pixelRange / 2.0));
    }

    public uint MsdfTextureSize { get; }
        
    public byte SampleRate { get; }
    
    // PixelRange: distance-field range in atlas texels = how far (in texels) from the contour the field
    // stays a usable gradient before it saturates to 0/1 (black/white). It is ALSO the width of the
    // colored field that fills the cell around each glyph: at pxRange=6 the field dies ~3 texels out, so
    // the cell reads as black; a wider range carries the colored field across the whole cell margin
    // (the msdf-atlas-gen look) and gives later shadow/glow/outline effects real distance to sample.
    // This does NOT blur text: screenPxRange() scales with PxRange, so the AA band stays ~1 screen pixel
    // at any range. The only cost of a larger range is coarser 8-bit precision of the gradient right at
    // the contour, which is negligible here. GlyphMargin auto-tracks this (= ceil(pxRange/2)).
    public byte PixelRange { get; }
        
    public uint StartGlyphIndex { get; }
        
    public uint GlyphCount { get; }

    public GlyphSortingVariant SortingVariant { get; }
    
    public GlyphPlacingVariant PlacingVariant { get; }
    
    public uint GlyphMargin { get; }

    public override bool Equals(object obj)
    {
        if (obj is FontParameters fontParameters)
        {
            return fontParameters.MsdfTextureSize == MsdfTextureSize &&
                   fontParameters.SampleRate == SampleRate &&
                   fontParameters.PixelRange == PixelRange &&
                   fontParameters.StartGlyphIndex == StartGlyphIndex &&
                   fontParameters.GlyphCount == GlyphCount &&
                   fontParameters.SortingVariant == SortingVariant &&
                   fontParameters.PlacingVariant == PlacingVariant &&
                   fontParameters.GlyphMargin == GlyphMargin;
        }

        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = 17;
            hashCode = hashCode * 23 + MsdfTextureSize.GetHashCode();
            hashCode = hashCode * 23 + SampleRate.GetHashCode();
            hashCode = hashCode * 23 + PixelRange.GetHashCode();
            hashCode = hashCode * 23 + StartGlyphIndex.GetHashCode();
            hashCode = hashCode * 23 + GlyphCount.GetHashCode();
            hashCode = hashCode * 23 + SortingVariant.GetHashCode();
            hashCode = hashCode * 23 + PlacingVariant.GetHashCode();
            hashCode = hashCode * 23 + GlyphMargin.GetHashCode();
            return hashCode;
        }
    }

    public static FontParameters Default(
        uint glyphTextureSize = 64,
        byte sampleRate = 5, 
        GlyphSortingVariant sortingVariant = GlyphSortingVariant.ByIndex,
        GlyphPlacingVariant placingVariant = GlyphPlacingVariant.Square)
    {
        const byte pixelRange = 16;
        const uint glyphMargin = 0;

        var fontParameters = new FontParameters(
            glyphTextureSize,
            sampleRate,
            pixelRange,
            0,
            UInt32.MaxValue,
            sortingVariant,
            placingVariant,
            glyphMargin
        );
        return fontParameters;
    }
}