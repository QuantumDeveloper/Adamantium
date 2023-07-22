namespace Adamantium.Imaging.Png
{
    public class PngEncoderSettings
    {
        /*automatically choose output PNG color type. Default: true*/
        public bool AutoConvert { get; set; } = true;

        /*If true, follows the official PNG heuristic: if the PNG uses a palette or lower than
        8 bit depth, set all filters to zero. Otherwise use the filter_strategy. Note that to
        completely follow the official PNG heuristic, FilterPaletteZero must be true and
        filter_strategy must be LFS_MINSUM*/
        public bool FilterPaletteZero { get; set; }

        /*Which filter strategy to use when not using zeroes due to filter_palette_zero.
        Set filter_palette_zero to 0 to ensure always using your chosen strategy. Default: LFS_MINSUM*/
        public FilterStrategy FilterStrategy { get; set; }

        // ZLIB settings
        /*the block type for LZ (0, 1, 2 or 3, see zlib standard). Should be 2 for proper compression.*/
        public uint BType { get; set; }

        /*whether or not to use LZ77. Should be 1 for proper compression.*/
        public bool UseLZ77 { get; set; }

        /// <summary>
        /// must be a power of two <= 32768 (2 in power 15). higher compresses more but is slower. Default value: 2048.*/
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Level0;

        /*mininum lz77 length. 3 is normally best, 6 can be better for some PNGs. Default: 0*/
        public uint MinMatch { get; set; } = 3;

        /*stop searching if >= this length found. Set to 258 for best compression. Default: 128*/
        public uint NiceMatch { get; set; } = 128;

        public bool LazyMatching { get; set; } = true;

        /*encode text chunks as zTXt chunks instead of tEXt chunks, and use compression in iTXt chunks*/
        public bool TextCompression { get; set; }

        /*force creating a PLTE chunk if colortype is 2 or 6 (= a suggested palette).
        If colortype is 3, PLTE is _always_ created.*/
        public bool ForcePalette { get; set; }
    }
}
