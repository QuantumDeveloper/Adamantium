using System;
using Adamantium.Fonts.TextureGeneration;

namespace Adamantium.Graphics.Fonts;

public class FontParameters
{
    public FontParameters(
        uint msdfTextureSize, 
        byte sampleRate, 
        byte pixelRange, 
        uint startGlyphIndex,
        uint glyphCount,
        GlyphSortingVariant sortingVariant,
        uint glyphMargin)
    {
        MsdfTextureSize = msdfTextureSize;
        SampleRate = sampleRate;
        PixelRange = pixelRange;
        StartGlyphIndex = startGlyphIndex;
        GlyphCount = glyphCount;
        SortingVariant = sortingVariant;
        GlyphMargin = glyphMargin;
    }

    public uint MsdfTextureSize { get; }
        
    public byte SampleRate { get; }
        
    public byte PixelRange { get; }
        
    public uint StartGlyphIndex { get; }
        
    public uint GlyphCount { get; }

    public GlyphSortingVariant SortingVariant { get; }
    
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
                   fontParameters.GlyphMargin == GlyphMargin;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MsdfTextureSize, SampleRate, PixelRange, StartGlyphIndex, GlyphCount, SortingVariant, GlyphMargin);
    }

    public static FontParameters Default(
        uint glyphTextureSize = 64, 
        byte sampleRate = 5, 
        GlyphSortingVariant sortingVariant = GlyphSortingVariant.ByIndex)
    {
        var fontParameters = new FontParameters(
            glyphTextureSize,
            sampleRate,
            6,
            0,
            UInt32.MaxValue, 
            sortingVariant,
            4
        );
        return fontParameters;
    }
}