using System;

namespace Adamantium.Fonts.Tables.WOFF
{
    internal class Woff2Header : WoffHeader
    {
        /// <summary>
        /// Total length of the compressed data block.
        /// </summary>
        public UInt32 TotalCompressedSize { get; set; }
    }
}