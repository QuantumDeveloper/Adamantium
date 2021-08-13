namespace Adamantium.Fonts.Tables.GPOS
{
    internal abstract class GPOSLookupSubTable : ILookupSubTable
    {
        public abstract GPOSLookupType Type { get; }
        public LookupOwnerType OwnerType => LookupOwnerType.GPOS;
        public virtual void SubstituteGlyphs(IGlyphSubstitutionLookup substitutionLookup, uint index)
        {
        }

        public virtual void PositionGlyph(IGlyphPositioningLookup glyphPositioningLookup, uint startIndex, uint length)
        {
        }
    }
}