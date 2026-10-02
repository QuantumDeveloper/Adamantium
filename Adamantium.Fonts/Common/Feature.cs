using System;
using System.Linq;
using Adamantium.Fonts.Tables.GPOS;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Common
{
    public class Feature
    {
        public Feature(FeatureInfo featureInfo)
        {
            Info = featureInfo;
        }
        
        public FeatureInfo Info { get; }
        
        public FeatureParametersTable FeatureParameters { get; internal set; }
        
        public bool IsEnabled { get; set; }
        
        internal ILookupTable[] Lookups { get; set; }
        
        public void Apply(GlyphLayoutContainer container, uint index, uint length)
        {
            if (!IsEnabled)
            {
                return;
            }
            
            if (container.IsFeatureApplied(Info.Tag)) return;
            
            container.FeatureApplied(Info.Tag);

            container.NewProcessingStart();

            foreach (var lookup in Lookups)
            {
                if (IsPairPositioning(lookup))
                {
                    PositionPairs(lookup, container, index, length);
                    continue;
                }

                foreach (var subTable in lookup.SubTables)
                {
                    switch (subTable.OwnerType)
                    {
                        case FeatureKind.GSUB:
                            subTable.SubstituteGlyphs(container, Info, index, length);
                            break;
                        case FeatureKind.GPOS:
                            subTable.PositionGlyph(container, Info, index, length);
                            break;
                    }

                    if (container.IsProcessingDone) return;
                }
            }
        }

        private static bool IsPairPositioning(ILookupTable lookup)
        {
            return lookup.SubTables.Length > 0 &&
                   lookup.SubTables.All(x => x is GPOSLookupSubTable { Type: GPOSLookupType.PairAdjustment });
        }

        private void PositionPairs(ILookupTable lookup, GlyphLayoutContainer container, uint index, uint length)
        {
            var endIndex = Math.Min(index + length, container.Count);
            for (var position = index; position < endIndex; position++)
            {
                foreach (GPOSLookupSubTable subTable in lookup.SubTables)
                {
                    if (subTable.PositionGlyphAt(container, Info, position))
                    {
                        break;
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"Short name: {Info.Tag}, Friendly name: {Info.FriendlyName}";
        }
    }
}