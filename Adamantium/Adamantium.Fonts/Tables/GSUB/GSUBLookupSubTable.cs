using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal abstract class GSUBLookupSubTable : LookupSubTableBase
    {
        public abstract GSUBLookupType Type { get; }
        public override FeatureKind OwnerType => FeatureKind.GSUB;
    }
}