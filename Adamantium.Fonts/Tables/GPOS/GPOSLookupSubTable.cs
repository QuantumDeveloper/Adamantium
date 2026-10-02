using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal abstract class GPOSLookupSubTable : LookupSubTableBase
    {
        public abstract GPOSLookupType Type { get; }
        public override FeatureKind OwnerType => FeatureKind.GPOS;

        internal virtual bool PositionGlyphAt(IGlyphPositioning glyphPositioning, FeatureInfo featureInfo, uint index)
        {
            return false;
        }
    }
}