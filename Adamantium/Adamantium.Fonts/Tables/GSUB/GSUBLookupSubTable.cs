using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal abstract class GSUBLookupSubTable : ILookupSubTable
    {
        public abstract GSUBLookupType Type { get; }
        public FeatureKind OwnerType => FeatureKind.GSUB;
        public virtual bool SubstituteGlyphs(FontLanguage language,
            FeatureInfo featureInfo,
            IGlyphSubstitutionLookup substitutionLookup,
            uint index)
        {
            return false;
        }

        public virtual void PositionGlyph(FontLanguage language, FeatureInfo featureInfo,
            IGlyphPositioningLookup glyphPositioningLookup,
            uint startIndex,
            uint length)
        {
        }
    }
}