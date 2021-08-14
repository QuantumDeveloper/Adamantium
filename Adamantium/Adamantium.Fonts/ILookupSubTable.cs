using Adamantium.Fonts.Common;

namespace Adamantium.Fonts
{
    public interface ILookupSubTable
    {
        public LookupOwnerType OwnerType { get; }

        public void SubstituteGlyphs(FeatureInfo featureInfo, IGlyphSubstitutionLookup substitutionLookup, uint index);

        public void PositionGlyph(FeatureInfo featureInfo, IGlyphPositioningLookup glyphPositioningLookup, uint startIndex, uint length);
    }
}