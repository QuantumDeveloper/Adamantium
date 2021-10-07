using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Tables.Layout
{
    public class LookupSubTableBase : ILookupSubTable
    {
        public FeatureKind OwnerType { get; }
        public virtual bool SubstituteGlyphs(
            IGlyphSubstitutions substitutions, 
            FeatureInfo featureInfo, 
            uint position, 
            uint length)
        {
            return false;
        }

        public virtual void PositionGlyph(
            IGlyphPositioning glyphPositioning, 
            FeatureInfo featureInfo, 
            uint startIndex,
            uint length)
        {
        }
    }
}