using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class ClassSequenceRuleTable
    {
        public UInt16[] InputSequence { get; set; }
        
        public SequenceLookupRecord[] LookupRecords { get; set; }
    }
}