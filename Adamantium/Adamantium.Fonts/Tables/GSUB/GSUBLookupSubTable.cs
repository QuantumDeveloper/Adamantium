namespace Adamantium.Fonts.Tables.GSUB
{
    internal abstract class GSUBLookupSubTable : ILookupSubTable
    {
        public abstract GSUBLookupType Type { get; }
        public LookupOwnerType OwnerType => LookupOwnerType.GSUB;
        public bool SubstituteGlyphs(IGlyphSubstitutionLookup substitutionLookup, uint index)
        {
            return false;
        }

        public bool PositionGlyph(IGlyphPositioningLookup glyphPositioningLookup, uint index)
        {
            return false;
        }
    }
}