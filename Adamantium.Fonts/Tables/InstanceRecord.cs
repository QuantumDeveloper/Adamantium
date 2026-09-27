using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables
{
    internal class InstanceRecord
    {
        public UInt16 SubfamilyNameID { get; set; }
        public UInt16 Flags { get; set; }
        public List<double> Coordinates { get; set; }
        
        // it is optional, omit for now (see https://docs.microsoft.com/en-us/typography/opentype/spec/fvar)
        //public UInt16 PostScriptNameID { get; set; }

        public String InstanceSubfamilyName { get; set; }
    }
}