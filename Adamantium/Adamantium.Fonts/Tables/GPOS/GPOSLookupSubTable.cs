namespace Adamantium.Fonts.Tables.GPOS
{
    internal abstract class GPOSLookupSubTable : ILookupSubTable
    {
        public abstract GPOSLookupType Type { get; }
        public LookupOwnerType OwnerType => LookupOwnerType.GPOS;
        public virtual bool SubstituteGlyphs(IGlyphSubstitutionLookup substitutionLookup, uint index)
        {
            return false;
        }

        public virtual bool PositionGlyph(IGlyphPositioningLookup glyphPositioningLookup, uint index)
        {
            return false;
        }
    }
}