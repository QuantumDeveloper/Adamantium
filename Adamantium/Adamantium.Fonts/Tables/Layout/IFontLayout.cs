namespace Adamantium.Fonts.Tables.Layout
{
    internal interface IFontLayout
    {
        /// <summary>
        /// Offset to ScriptList table, from beginning of GPOS/GSUB table
        /// </summary>
        public ScriptTable[] ScriptList { get; set; }
        
        /// <summary>
        /// Offset to FeatureList table, from beginning of GPOS/GSUB table
        /// </summary>
        public FeatureTable[] FeatureList { get; set; }
        
        public ILookupTable[] LookupList { get; set; }
    }
}