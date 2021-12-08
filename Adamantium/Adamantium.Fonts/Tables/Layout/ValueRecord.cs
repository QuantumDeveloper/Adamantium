namespace Adamantium.Fonts.Tables.Layout
{
    internal class ValueRecord
    {
        //All fields are optional
        
        /// <summary>
        /// Horizontal adjustment for placement, in design units.
        /// </summary>
        public short XPlacement { get; set; }
        
        /// <summary>
        /// Vertical adjustment for placement, in design units.
        /// </summary>
        public short YPlacement { get; set; }
        
        /// <summary>
        /// Horizontal adjustment for advance, in design units — only used for horizontal layout.
        /// </summary>
        public short XAdvance { get; set; }
        
        /// <summary>
        /// Vertical adjustment for advance, in design units — only used for vertical layout.
        /// </summary>
        public short YAdvance { get; set; }
        
        /// <summary>
        /// Offset to Device table (non-variable font) / VariationIndex table (variable font) for horizontal placement,
        /// from beginning of the immediate parent table (SinglePos or PairPosFormat2 lookup subtable,
        /// PairSet table within a PairPosFormat1 lookup subtable) — may be NULL.
        /// </summary>
        public ushort XPlacementDevice { get; set; }
        
        /// <summary>
        /// Offset to Device table (non-variable font) / VariationIndex table (variable font) for vertical placement,
        /// from beginning of the immediate parent table (SinglePos or PairPosFormat2 lookup subtable,
        /// PairSet table within a PairPosFormat1 lookup subtable) — may be NULL.
        /// </summary>
        public ushort YPlacementDevice { get; set; }
        
        /// <summary>
        /// Offset to Device table (non-variable font) / VariationIndex table (variable font) for horizontal advance,
        /// from beginning of the immediate parent table (SinglePos or PairPosFormat2 lookup subtable,
        /// PairSet table within a PairPosFormat1 lookup subtable) — may be NULL.
        /// </summary>
        public ushort XAdvanceDevice { get; set; }
        
        /// <summary>
        /// Offset to Device table (non-variable font) / VariationIndex table (variable font) for vertical advance,
        /// from beginning of the immediate parent table (SinglePos or PairPosFormat2 lookup subtable,
        /// PairSet table within a PairPosFormat1 lookup subtable) — may be NULL.
        /// </summary>
        public ushort YAdvanceDevice { get; set; }
    }
}