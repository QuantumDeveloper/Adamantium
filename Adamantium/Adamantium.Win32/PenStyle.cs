namespace Adamantium.Win32
{
    public enum PenStyle : int
    {
        /// <summary>
        /// The pen is solid.
        /// </summary>
        Solid = 0,

        /// <summary>
        /// The pen is dashed. This style is valid only when the pen width is one or less in device units.
        /// </summary>
        Dash = 1,

        /// <summary>
        /// The pen is dotted. This style is valid only when the pen width is one or less in device units.
        /// </summary>
        Dot = 2,

        /// <summary>
        /// The pen has alternating dashes and dots. This style is valid only when the pen width is one or less in device units.
        /// </summary>
        DashDot = 3,

        /// <summary>
        /// The pen has alternating dashes and double dots. This style is valid only when the pen width is one or less in device units.
        /// </summary>
        DashDotDot = 4,

        /// <summary>
        /// The pen is invisible.
        /// </summary>
        Null = 5,

        /// <summary>
        /// The pen is solid. When this pen is used in any GDI drawing function that takes a bounding rectangle, the dimensions of the figure are shrunk so that it fits entirely in the bounding rectangle, taking into account the width of the pen. This applies only to geometric pens.
        /// </summary>
        InsideFrame = 6
    }
}