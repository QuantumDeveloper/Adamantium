using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class ContextualPositioningSubtableFormat1 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.ContextPositioning;
        
        public CoverageTable Coverage { get; set; }
        
        public SequenceRuleSetTable[] SequenceRuleSets { get; set; } 

    }
}