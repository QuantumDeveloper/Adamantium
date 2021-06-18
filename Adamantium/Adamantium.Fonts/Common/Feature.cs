using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Common
{
    public class Feature
    {
        public string FriendlyName { get; private set; }
        
        public string ShortName { get; private set; }
        
        public FeatureParametersTable FeatureParameters { get; set; }
        
        internal ILookupTable[] Lookups { get; set; }

        public void Apply(TypeFace typeface)
        {
            foreach (var lookup in Lookups)
            {
                foreach (var subTable in lookup.SubTables)
                {
                    switch (subTable.OwnerType)
                    {
                        case LookupOwnerType.GSUB:
                            break;
                        case LookupOwnerType.GPOS:
                            break;
                    }
                }
            }
        }
    }
}