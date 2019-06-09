namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public enum FilterStrategy
    {
        /// <summary>
        /// Every filter at zero
        /// </summary>
        Zero,
        /// <summary>
        /// Use filter that gives minimum sum, as described in the official PNG filter heuristic.
        /// </summary>
        MinSum,
        /// <summary>
        /// Use the filter type that gives smallest Shannon entropy for this scanline. Depending
        /// on the image, this is better or worse than minsum.
        /// </summary>
        Entropy,
        /// <summary>
        /// Brute-force-search PNG filters by compressing each filter for each scanline.
        /// Experimental, very slow, and only rarely gives better compression than MINSUM.
        /// </summary>
        BruteForce
    }
}
