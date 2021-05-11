using System;
using System.Runtime.InteropServices;

namespace Adamantium.Fonts.Tables
{
    [StructLayout(LayoutKind.Sequential)]
    public struct EncodingRecord
    {
        public UInt16 PlatformId { get; set; }
        
        public UInt16 EncodingId { get; set; }
        
        public UInt32 SubtableOffset { get; set; }

        public override string ToString()
        {
            return $"PlatformID: {PlatformId}, EncodingId: {EncodingId}, SubtableOffset: {SubtableOffset}";
        }
    }
}