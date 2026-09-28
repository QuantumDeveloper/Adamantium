using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal abstract class GPOSLookupSubTable : LookupSubTableBase
    {
        public abstract GPOSLookupType Type { get; }
        public override FeatureKind OwnerType => FeatureKind.GPOS;
    }
}