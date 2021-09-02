using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class LigatureSubstitutionSubTable : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Ligature;
        
        public CoverageTable Coverage { get; set; }
        
        public LigatureSetTable[] LigatureSetTables { get; set; }

        public override bool SubstituteGlyphs(FontLanguage language, FeatureInfo featureInfo, IGlyphSubstitutionLookup substitutionLookup,
            uint index)
        {

            var foundPos = Coverage.FindPosition((ushort)index);
            if (foundPos > -1)
            {
                var ligatureTable = LigatureSetTables[foundPos];
                foreach (var ligature in ligatureTable.Ligatures)
                {
                    int componentLen = ligature.ComponentGlypIDs.Length;
                    bool allMatched = true;
                    var tmpIndex = index + 1;
                    for (int i = 0; i < componentLen; ++i)
                    {
                        if (tmpIndex + i != ligature.ComponentGlypIDs[i])
                        {
                            allMatched = false;
                            break;
                        }
                    }

                    if (allMatched)
                    {
                        substitutionLookup.Replace(index, componentLen + 1, ligature.LigatureGlyphID);
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}