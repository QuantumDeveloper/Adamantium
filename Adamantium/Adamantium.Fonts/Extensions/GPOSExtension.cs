namespace Adamantium.Fonts.Extensions
{
    public static class GPOSExtension
    {
        public static int FindGlyphBackwardByKind(this IGlyphPositioning glyphPositioning,
            GlyphClassDefinition definition, uint startIndex, uint endIndex)
        {
            for (var i = startIndex; i >= endIndex; --i)
            {
                if (glyphPositioning.GetGlyphClassDefinition(i) == definition)
                {
                    return (int) i;
                }
            }

            return -1;
        }
    }
}