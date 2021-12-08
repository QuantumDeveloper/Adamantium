using System;

namespace Adamantium.Fonts.Tables.Layout
{
    [Flags]
    public enum ValueFormat : ushort
    {
        /// <summary>
        /// Includes horizontal adjustment for placement
        /// </summary>
        XPlacement	        = 0x0001,
        
        /// <summary>
        /// Includes vertical adjustment for placement
        /// </summary>
        YPlacement	        = 0x0002,
        
        /// <summary>
        /// Includes horizontal adjustment for advance
        /// </summary>
        XAdvance	        = 0x0004,
        
        /// <summary>
        /// Includes vertical adjustment for advance
        /// </summary>
        YAdvance	        = 0x0008,
        
        /// <summary>
        /// Includes Device table (non-variable font) / VariationIndex table (variable font) for horizontal placement
        /// </summary>
        XPlacementDevice  = 0x0010,
        
        /// <summary>
        /// Includes Device table (non-variable font) / VariationIndex table (variable font) for vertical placement
        /// </summary>
        YPlacementDevice  = 0x0020,
        
        /// <summary>
        /// Includes Device table (non-variable font) / VariationIndex table (variable font) for horizontal advance
        /// </summary>
        XAdvanceDevice    = 0x0040,
        
        /// <summary>
        /// Includes Device table (non-variable font) / VariationIndex table (variable font) for vertical advance
        /// </summary>
        YAdvanceDevice    = 0x0080,
        
        /// <summary>
        /// For future use (set to zero)
        /// </summary>
        Reserved	        = 0xFF00,
    }
}