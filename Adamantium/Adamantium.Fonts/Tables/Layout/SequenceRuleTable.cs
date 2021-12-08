using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class SequenceRuleTable
    {
        /// <summary>
        /// Number of glyphs in the input glyph sequence
        /// </summary>
        /// <returns></returns>
        public UInt16 GlyphCount { get; set; }
        
        /// <summary>
        /// Array of input glyph IDs—starting with the second glyph
        /// </summary>
        public UInt16[] InputSequence { get; set; }

        /// <summary>
        /// Array of Sequence lookup records
        /// </summary>
        public SequenceLookupRecord[] SeqLookupRecords { get; set; }
    }
}