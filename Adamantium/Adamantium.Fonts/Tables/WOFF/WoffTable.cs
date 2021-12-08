using System;

namespace Adamantium.Fonts.Tables.WOFF
{
    internal class WoffTable
    {
        /// <summary>
        /// 4-byte sfnt table identifier.
        /// </summary>
        public String Tag { get; set; }
        
        /// <summary>
        /// Offset to the data, from beginning of WOFF file.
        /// </summary>
        /// <returns></returns>
        public UInt32 Offset { get; set; }
        
        /// <summary>
        /// Length of the compressed data, excluding padding.
        /// </summary>
        /// <returns></returns>
        public UInt32 CompLength { get; set; }
        
        /// <summary>
        /// Length of the uncompressed table, excluding padding.
        /// </summary>
        public UInt32 OrigLength { get; set; }
        
        /// <summary>
        /// Checksum of the uncompressed table.
        /// </summary>
        public UInt32 OrigChecksum { get; set; }
        
        public Int64 ExpectedOffset { get; set; }
    }
}