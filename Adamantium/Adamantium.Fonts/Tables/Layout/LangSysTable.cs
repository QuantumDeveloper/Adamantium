using System;
using Adamantium.Fonts.Extensions;

namespace Adamantium.Fonts.Tables.Layout
{
    /// <summary>
    /// The Language System table (LangSys) identifies language-system features
    /// used to render the glyphs in a script.
    /// </summary>
    internal class LangSysTable
    {
        public uint Tag { get; }
        
        public string Name { get; }
        
        /// <summary>
        /// NULL (reserved for an offset to a reordering table)
        /// </summary>
        public UInt16 LookupOrderOffset { get; set; }
        
        /// <summary>
        /// Index of a feature required for this language system; if no required features = 0xFFFF
        /// </summary>
        public UInt16 RequiredFeatureIndex { get; set; }
        
        /// <summary>
        /// Array of indices into the FeatureList, in arbitrary order
        /// </summary>
        public UInt16[] FeatureIndices { get; set; }
        
        public bool HasRequireFeature => RequiredFeatureIndex != 0xFFFF;

        public LangSysTable(uint tag)
        {
            Tag = tag;
            Name = tag.GetString();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}