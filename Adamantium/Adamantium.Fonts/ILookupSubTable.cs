using Adamantium.Fonts.Common;

namespace Adamantium.Fonts
{
    public interface ILookupSubTable
    {
        public FeatureKind OwnerType { get; }

        public bool SubstituteGlyphs(
            IGlyphSubstitutions substitutions,
            FeatureInfo featureInfo,
            uint index,
            uint length);

        public void PositionGlyph(
            IGlyphPositioning glyphPositioning,
            FeatureInfo featureInfo,
            uint startIndex,
            uint length);
    }
}