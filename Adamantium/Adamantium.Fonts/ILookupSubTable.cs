namespace Adamantium.Fonts
{
    public interface ILookupSubTable
    {
        public LookupOwnerType OwnerType { get; }

        public void SubstituteGlyphs(IGlyphSubstitutionLookup substitutionLookup, uint index);

        public void PositionGlyph(IGlyphPositioningLookup glyphPositioningLookup, uint startIndex, uint length);
    }
}