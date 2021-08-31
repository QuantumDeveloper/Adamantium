using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal abstract class GPOSLookupSubTable : ILookupSubTable
    {
        public abstract GPOSLookupType Type { get; }
        public FeatureKind OwnerType => FeatureKind.GPOS;
        public virtual void SubstituteGlyphs(FontLanguage language,
            FeatureInfo featureInfo,
            IGlyphSubstitutionLookup substitutionLookup,
            uint index)
        {
        }

        public virtual void PositionGlyph(FontLanguage language,
            FeatureInfo featureInfo,
            IGlyphPositioningLookup glyphPositioningLookup,
            uint startIndex,
            uint length)
        {
        }
    }
}