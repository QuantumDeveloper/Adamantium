using Adamantium.Fonts.Common;

namespace Adamantium.Fonts
{
    public interface ILookupSubTable
    {
        public FeatureKind OwnerType { get; }

        public bool SubstituteGlyphs(
            FontLanguage language,
            FeatureInfo featureInfo,
            IGlyphSubstitutionLookup substitutionLookup,
            uint index);

        public void PositionGlyph(
            FontLanguage language,
            FeatureInfo featureInfo,
            IGlyphPositioningLookup glyphPositioningLookup,
            uint startIndex,
            uint length);

    }
}