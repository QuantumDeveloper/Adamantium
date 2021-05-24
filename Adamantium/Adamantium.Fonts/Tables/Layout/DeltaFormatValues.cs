namespace Adamantium.Fonts.Tables.Layout
{
    internal enum DeltaFormatValues
    {
        /// <summary>
        /// Signed 2-bit value, 8 values per uint16
        /// </summary>
        Local2BitDeltas = 0x0001,
        
        /// <summary>
        /// Signed 4-bit value, 4 values per uint16
        /// </summary>
        Local4BitDeltas = 0x0002,
        
        /// <summary>
        /// Signed 8-bit value, 2 values per uint16
        /// </summary>
        Local8BitDeltas = 0x0003,
        
        /// <summary>
        /// VariationIndex table, contains a delta-set index pair.
        /// </summary>
        VariationIndex	= 0x8000,
        
        /// <summary>
        /// For future use — set to 0
        /// </summary>
        Reserved	     = 0x7FFC,
    }
}