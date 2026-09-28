using System;

namespace Adamantium.Fonts.Tables
{
    internal class CaretValue
    {
        public UInt16 Format { get; set; }
        
        /// <summary>
        /// X or Y value, in design units
        /// <remarks>Format 1 and 3</remarks>
        /// </summary>
        public Int16 Coordinate { get; set; }
        
        /// <summary>
        /// Contour point index on glyph
        /// <remarks>Format 2</remarks>
        /// </summary>
        public UInt16 PointIndex { get; set; }
        
        /// <summary>
        /// Offset to Device table (non-variable font) / Variation Index table (variable font) for X or Y value-from beginning of CaretValue table
        /// <remarks>Format 3</remarks>
        /// </summary>
        public Int64 DeviceOffset { get; set; }
    }
}