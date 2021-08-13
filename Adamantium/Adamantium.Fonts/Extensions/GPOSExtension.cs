namespace Adamantium.Fonts.Extensions
{
    public static class GPOSExtension
    {
        public static int FindGlyphBackwardByKind(this IGlyphPositioningLookup glyphPositioningLookup,
            GlyphClassDefinition definition, uint startIndex, uint endIndex)
        {
            for (var i = startIndex; i >= endIndex; --i)
            {
                if (glyphPositioningLookup.GetGlyphClassDefinition(i) == definition)
                {
                    return (int) i;
                }
            }

            return -1;
        }
    }
}