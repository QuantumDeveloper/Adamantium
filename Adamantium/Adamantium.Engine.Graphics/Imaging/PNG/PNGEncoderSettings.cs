using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGEncoderSettings
    {
        /*automatically choose output PNG color type. Default: true*/
        public bool AutoConvert { get; set; } = true;

        /*If true, follows the official PNG heuristic: if the PNG uses a palette or lower than
        8 bit depth, set all filters to zero. Otherwise use the filter_strategy. Note that to
        completely follow the official PNG heuristic, FilterPaletteZero must be true and
        filter_strategy must be LFS_MINSUM*/
        public bool FilterPaletteZero { get; set; }

        public FilterStrategy FilterStrategy { get; set; }

        // ZLIB settings
        /*the block type for LZ (0, 1, 2 or 3, see zlib standard). Should be 2 for proper compression.*/
        public uint BType { get; set; }

        /*whether or not to use LZ77. Should be 1 for proper compression.*/
        public bool UseLZ77 { get; set; }

        /*must be a power of two <= 32768. higher compresses more but is slower. Default value: 2048.*/
        public int WindowSize { get; set; } = 2048;

        /*mininum lz77 length. 3 is normally best, 6 can be better for some PNGs. Default: 0*/
        public uint MinMatch { get; set; }

        /*stop searching if >= this length found. Set to 258 for best compression. Default: 128*/
        public uint NiceMatch { get; set; } = 128;

        public bool LazyMatching { get; set; } = true;
    }

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
        BruteForce,
    }
}
