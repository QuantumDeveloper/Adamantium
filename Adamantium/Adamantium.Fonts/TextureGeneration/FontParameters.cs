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
        GlyphMargin = glyphMargin;
    }

    public uint MsdfTextureSize { get; }
        
    public byte SampleRate { get; }
        
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
        var fontParameters = new FontParameters(
            glyphTextureSize,
            sampleRate,
            6,
            0,
            UInt32.MaxValue, 
            sortingVariant,
            placingVariant,
            0
        );
        return fontParameters;
    }
}