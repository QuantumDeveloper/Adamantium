using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class ChainedSequenceRuleTable
    {
        /// <summary>
        /// Array of backtrack glyph IDs
        /// </summary>
        /// <returns></returns>
        public UInt16[] BacktrackSequence { get; set; }

        /// <summary>
        /// Array of input glyph IDs—start with second glyph
        /// </summary>
        /// <returns></returns>
        public UInt16[] InputSequence { get; set; }

        /// <summary>
        /// Array of lookahead glyph IDs
        /// </summary>
        /// <returns></returns>
        public UInt16[] LookaheadSequence { get; set; }

        /// <summary>
        /// Array of SequenceLookupRecords
        /// </summary>
        public SequenceLookupRecord[] SeqLookupRecords { get; set; }
    }
}