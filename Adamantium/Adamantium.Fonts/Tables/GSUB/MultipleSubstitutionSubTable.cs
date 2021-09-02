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
            FontLanguage language, 
            FeatureInfo featureInfo, 
            IGlyphSubstitutionLookup substitutionLookup,
            uint index)
        {
            var foundPosition = Coverage.FindPosition((ushort)index);
            if (foundPosition > -1)
            {
                var seqTable = SequenceTables[foundPosition];
                substitutionLookup.Replace(index, seqTable.SubstituteGlyphIDs);
                return true;
            }
            
            return false;
        }
    }
    
    
}