using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal abstract class GPOSLookupSubTable : ILookupSubTable
    {
        public abstract GPOSLookupType Type { get; }
        public FeatureKind OwnerType => FeatureKind.GPOS;
        public virtual bool SubstituteGlyphs(FontLanguage language,
            FeatureInfo featureInfo,
            IGlyphSubstitutionLookup substitutionLookup,
            uint index)
        {
            return false;
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