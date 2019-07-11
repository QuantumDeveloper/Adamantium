namespace Adamantium.Imaging.Png
{
    public enum RenderingIntent : byte
    {
        /// <summary>
        /// Perceptual intent is for images preferring good adaptation to the output device 
        /// gamut at the expense of colorimetric accuracy, like photographs.
        /// </summary>
        Perceptual = 0,

        /// <summary>
        /// Relative colorimetric intent is for images requiring color appearance matching 
        /// (relative to the output device white point), like logos.
        /// </summary>
        RelativeColorimetric = 1,

        /// <summary>
        /// Saturation intent is for images preferring preservation of saturation at the expense of 
        /// hue and lightness, like charts and graphs.
        /// </summary>
        Saturation = 2,

        /// <summary>
        /// Absolute colorimetric intent is for images requiring preservation of absolute colorimetry, 
        /// like proofs (previews of images destined for a different output device).
        /// </summary>
        AbsoluteColorimetric = 3
    }
}
