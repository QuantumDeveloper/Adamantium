using System;

namespace Adamantium.Imaging.Png
{
    internal class PNGColorProfile
    {
        public PNGColorProfile()
        {
            Palette = new byte[1024];
        }
        /*not grayscale*/
        public bool Colored { get; set; }
        /*image is not opaque and color key is possible instead of full alpha*/
        public bool Key { get; set; }
        /*key values, always as 16-bit, in 8-bit case the byte is duplicated, e.g. 65535 means 255*/
        public ushort KeyR { get; set; }
        public ushort KeyG { get; set; }
        public ushort KeyB { get; set; }
        /*image is not opaque and alpha channel or alpha palette required*/
        public bool Alpha { get; set; }
        /*amount of colors, up to 257. Not valid if bits == 16.*/
        public uint Numcolors { get; set; }
        /*Remembers up to the first 256 RGBA colors, in no particular order*/
        public byte[] Palette { get; set; }
        /*bits per channel (not for palette). 1,2 or 4 for grayscale only. 16 if 16-bit per channel required.*/
        public uint Bits { get; set; }
        public uint Numpixels { get; set; }

        public void Add(uint r, uint g, uint b, uint a)
        {
            byte[] image = new byte[8];
            PNGColorMode mode = new PNGColorMode();
            image[0] = (byte)(r >> 8);
            image[1] = (byte)r;
            image[2] = (byte)(g >> 8);
            image[3] = (byte)g;
            image[4] = (byte)(b >> 8);
            image[5] = (byte)b;
            image[6] = (byte)(a >> 8);
            image[7] = (byte)a;
            mode.BitDepth = 16;
            mode.ColorType = PNGColorType.RGBA;
            PNGColorConversion.GetColorProfile(this, image, 1, 1, mode);
        }

        public static void AddPalette(PNGColorMode info, byte r, byte g, byte b, byte a)
        {
            byte[] data;
            /*the same resize technique as C++ std::vectors is used, and here it's made so that for a palette with
            the max of 256 colors, it'll have the exact alloc size*/
            if (info.Palette == null) /*allocate palette if empty*/
            {
                /*room for 256 colors with 4 bytes each*/
                data = new byte[1024];
                info.Palette = data;
            }
            info.Palette[4 * info.PaletteSize + 0] = r;
            info.Palette[4 * info.PaletteSize + 1] = g;
            info.Palette[4 * info.PaletteSize + 2] = b;
            info.Palette[4 * info.PaletteSize + 3] = a;
            ++info.PaletteSize;
        }

        /*Automatically chooses color type that gives smallest amount of bits in the
        output image, e.g. gray if there are only grayscale pixels, palette if there
        are less than 256 colors, color key if only single transparent color, ...
        Updates values of mode with a potentially smaller color model. mode_out should
        contain the user chosen color model, but will be overwritten with the new chosen one.*/
        public static void AutoChooseColor(PNGColorMode modeOut, byte[] image, uint width, uint height, PNGColorMode modeIn)
        {
            PNGColorProfile profile = new PNGColorProfile();
            PNGColorConversion.GetColorProfile(profile, image, width, height, modeIn);
            AutoChooseColorFromProfile(modeOut, modeIn, profile);
        }

        /*Autochoose color model given the computed profile. mode_in is to copy palette order from
        when relevant.*/
        public static void AutoChooseColorFromProfile(PNGColorMode modeOut, PNGColorMode modeIn, PNGColorProfile prof)
        {
            bool paletteOk;
            int paletteBits = 0;
            int i = 0;
            uint n = 0;
            var numpixels = prof.Numpixels;

            var alpha = prof.Alpha;
            var key = prof.Key;
            var bits = prof.Bits;

            modeOut.IsKeyDefined = false;

            if (key && numpixels <= 16)
            {
                alpha = true; /*too few pixels to justify tRNS chunk overhead*/
                key = false;
                if (bits < 8) bits = 8; /*PNG has no alphachannel modes with less than 8-bit per channel*/
            }
            n = prof.Numcolors;
            paletteBits = n <= 2 ? 1 : (n <= 4 ? 2 : (n <= 16 ? 4 : 8));
            paletteOk = n <= 256 && bits <= 8;
            if (numpixels < n * 2) paletteOk = false; /*don't add palette overhead if image has only a few pixels*/
            if (!prof.Colored && bits <= paletteBits) paletteOk = false; /*gray is less overhead*/

            if (paletteOk)
            {
                var p = prof.Palette;
                modeOut.Palette = null;
                modeOut.PaletteSize = 0;
                for (i = 0; i != prof.Numcolors; ++i)
                {
                    AddPalette(modeOut, p[i * 4 + 0], p[i * 4 + 1], p[i * 4 + 2], p[i * 4 + 3]);
                }

                modeOut.ColorType = PNGColorType.Palette;
                modeOut.BitDepth = (uint)paletteBits;

                if (modeIn.ColorType == PNGColorType.Palette && modeIn.PaletteSize >= modeOut.PaletteSize
                    && modeIn.BitDepth == modeOut.BitDepth)
                {
                    /*If input should have same palette colors, keep original to preserve its order and prevent conversion*/
                    modeOut.Palette = new byte[modeIn.Palette.Length];
                    Array.Copy(modeIn.Palette, modeOut.Palette, modeIn.Palette.Length);
                    modeOut.PaletteSize = modeIn.PaletteSize;
                }
            }
            else /*8-bit or 16-bit per channel*/
            {
                modeOut.BitDepth = bits;
                modeOut.ColorType = alpha ? (prof.Colored ? PNGColorType.RGBA : PNGColorType.GreyAlpha)
                    : (prof.Colored ? PNGColorType.RGB : PNGColorType.Grey);

                if (key)
                {
                    var mask = (1 << (int)modeOut.BitDepth) - 1; /*profile always uses 16-bit, mask converts it*/
                    modeOut.KeyR = (uint)(prof.KeyR & mask);
                    modeOut.KeyG = (uint)(prof.KeyG & mask);
                    modeOut.KeyB = (uint)(prof.KeyB & mask);
                    modeOut.IsKeyDefined = true;
                }
            }
        }
    }
}
