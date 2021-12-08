namespace Adamantium.Fonts.Tables.CFF
{
    internal class VariationStore
    {
        public VariationStore(VariationRegionList regionList, ItemVariationDataSubtable[] variationData)
        {
            VariationRegionList = regionList;
            ItemVariationData = variationData;
        }
        
        public VariationRegionList VariationRegionList { get; }
        
        public ItemVariationDataSubtable[] ItemVariationData { get; }
    }
}