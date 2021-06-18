namespace Adamantium.Fonts
{
    public interface ILookupSubTable
    {
        public LookupOwnerType OwnerType { get; }

        public bool SubstituteGlyphs(IGlyphSubstitutionLookup substitutionLookup, uint index);

        public bool PositionGlyph(IGlyphPositioningLookup glyphPositioningLookup, uint index);
    }
}