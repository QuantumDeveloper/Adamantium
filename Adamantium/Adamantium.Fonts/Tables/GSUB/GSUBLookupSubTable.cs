namespace Adamantium.Fonts.Tables.GSUB
{
    internal abstract class GSUBLookupSubTable : ILookupSubTable
    {
        public abstract GSUBLookupType Type { get; }
        public LookupOwnerType OwnerType => LookupOwnerType.GSUB;
        public void SubstituteGlyphs(IGlyphSubstitutionLookup substitutionLookup, uint index)
        {
            
        }

        public void PositionGlyph(IGlyphPositioningLookup glyphPositioningLookup, uint startIndex, uint length)
        {
            
        }
    }
}