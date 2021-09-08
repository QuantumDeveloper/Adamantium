using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class MultipleSubstitutionSubTable : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Multiple;
        
        public CoverageTable Coverage { get; set; }
        
        public SequenceTable[] SequenceTables { get; set; }

        public override bool SubstituteGlyphs(
            IGlyphSubstitutions substitutions,
            FeatureInfo featureInfo, 
            uint position,
            uint length)
        {
            var foundPosition = Coverage.FindPosition((ushort)substitutions.GetGlyphIndex(position));
            if (foundPosition > -1)
            {
                var seqTable = SequenceTables[foundPosition];
                substitutions.Replace(featureInfo, position, seqTable.SubstituteGlyphIDs);
                return true;
            }
            
            return false;
        }
    }
    
    
}