using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class ContextualPositioningSubtableFormat2 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.ContextPositioning;
        
        public CoverageTable Coverage { get; set; }
        
        public ClassDefTable ClassDef { get; set; }
        
        public ClassSequenceRuleSetTable[] ClassSequenceRuleSets { get; set; } 

    }
}