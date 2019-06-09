using System;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGColorMode
    {
        public PNGColorMode()
        {

        }

        public PNGColorMode(PNGColorMode copy)
        {
            ColorType = copy.ColorType;
            BitDepth = copy.BitDepth;
            if (copy.Palette != null)
            {
                Palette = new byte[Palette.Length];
                Array.Copy(copy.Palette, Palette, copy.Palette.Length);
            }
            PaletteSize = copy.PaletteSize;
            IsKeyDefined = copy.IsKeyDefined;
            KeyR = copy.KeyR;
            KeyG = copy.KeyG;
            KeyB = copy.KeyB;
        }

        public static PNGColorMode Create(PNGColorType colorType, uint bitDepth)
        {
            PNGColorMode colorMode = new PNGColorMode();
            colorMode.ColorType = colorType;
            colorMode.BitDepth = bitDepth;
            return colorMode;
        }

        /*color type, see PNG standard or documentation further in this header file*/
        public PNGColorType ColorType { get; set; }

        /*bits per sample, see PNG standard or documentation further in this header file*/
        public uint BitDepth { get; set; }

        /*
        palette (PLTE and tRNS)
        Dynamically allocated with the colors of the palette, including alpha.
        When encoding a PNG, to store your colors in the palette of the LodePNGColorMode, first use
        lodepng_palette_clear, then for each color use lodepng_palette_add.
        If you encode an image without alpha with palette, don't forget to put value 255 in each A byte of the palette.
        When decoding, by default you can ignore this palette, since LodePNG already
        fills the palette colors in the pixels of the raw RGBA output.
        The palette is only supported for PNGColorType.Palette.
        */

        /*palette in RGBARGBA... order. When allocated, must be either 0, or have size 1024*/
        public byte[] Palette { get; set; }
        /*palette size in number of colors (amount of bytes is 4 * palettesize)*/
        public Int64 PaletteSize { get; set; }

        /*
        transparent color key (tRNS)
        This color uses the same bit depth as the bitdepth value in this struct, which can be 1-bit to 16-bit.
        For grayscale PNGs, r, g and b will all 3 be set to the same.
        When decoding, by default you can ignore this information, since LodePNG sets
        pixels with this key to transparent already in the raw RGBA output.
        The color key is only supported for color types Grey and RGB.
        */

        /*is a transparent color key given? 0 = false, 1 = true*/
        public bool IsKeyDefined;

        /*red/grayscale component of color key*/
        public uint KeyR;
        /*green component of color key*/
        public uint KeyG;
        /*blue component of color key*/
        public uint KeyB;

        public static bool operator ==(PNGColorMode left, PNGColorMode right)
        {
            if (left.ColorType == right.ColorType && left.BitDepth == right.BitDepth
                && left.PaletteSize == right.PaletteSize && left.IsKeyDefined == right.IsKeyDefined
                && left.KeyR == right.KeyR && left.KeyG == right.KeyG && left.KeyB == right.KeyB)
            {
                return true;
            }

            return false;
        }

        public static bool operator !=(PNGColorMode left, PNGColorMode right)
        {
            if (left == right)
            {
                return false;
            }

            return true;
        }
    }
}
