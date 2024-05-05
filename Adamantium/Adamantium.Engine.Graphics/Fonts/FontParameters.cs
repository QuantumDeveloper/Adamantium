using System;

namespace Adamantium.Engine.Graphics.Fonts;

public class FontParameters
{
    public FontParameters(
        uint msdfTextureSize, 
        byte sampleRate, 
        byte pixelRange, 
        uint startGlyphIndex,
        uint glyphCount)
    {
        MsdfTextureSize = msdfTextureSize;
        SampleRate = sampleRate;
        PixelRange = pixelRange;
        StartGlyphIndex = startGlyphIndex;
        GlyphCount = glyphCount;
    }

    public uint MsdfTextureSize { get; }
        
    public byte SampleRate { get; }
        
    public byte PixelRange { get; }
        
    public uint StartGlyphIndex { get; }
        
    public uint GlyphCount { get; }

    public override bool Equals(object obj)
    {
        if (obj is FontParameters fontParameters)
        {
            return fontParameters.MsdfTextureSize == MsdfTextureSize &&
                   fontParameters.SampleRate == SampleRate &&
                   fontParameters.PixelRange == PixelRange &&
                   fontParameters.StartGlyphIndex == StartGlyphIndex &&
                   fontParameters.GlyphCount == GlyphCount;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MsdfTextureSize, SampleRate, PixelRange, StartGlyphIndex, GlyphCount);
    }
}