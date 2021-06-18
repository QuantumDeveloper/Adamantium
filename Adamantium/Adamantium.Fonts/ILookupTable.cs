using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts
{
    public interface ILookupTable
    {
        public UInt16 LookupType { get; }
        
        public LookupOwnerType OwnerType { get; }
        
        public LookupFlags LookupFlag { get; }
        
        public ILookupSubTable[] SubTables { get; }
        
        public UInt16 MarkFilteringSet { get; }
    }
}