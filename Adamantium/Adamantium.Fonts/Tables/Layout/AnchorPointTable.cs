using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class AnchorPointTable
    {
        public UInt16 Format { get; set; }
        
        // Format 1
        
        /// <summary>
        /// Horizontal value, in design units
        /// </summary>
        public Int16 XCoordinate { get; set; }
        
        /// <summary>
        /// Vertical value, in design units
        /// </summary>
        public Int16 YCoordinate { get; set; }
        
        // Format 2
        
        /// <summary>
        /// Index to glyph contour point
        /// </summary>
        public UInt16 AnchorPoint { get; set; }
        
        // Format 3
        
        /// <summary>
        /// Offset to Device table (non-variable font) / VariationIndex table (variable font) for X coordinate,
        /// from beginning of Anchor table (may be NULL)
        /// </summary>
        
        public DeviceTable XDevice { get; set; }
        
        public DeviceTable YDevice { get; set; }
    }
}