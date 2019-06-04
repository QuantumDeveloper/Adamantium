using Adamantium.Engine.Graphics.Imaging.PNG.Chunks;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGDecoderSettings
    {
        public PNGDecoderSettings()
        {
            ColorСonvert = true;
            ReadTextChunks = true;
        }

        /*if true, continue and don't give an error message if the Adler32 checksum is corrupted*/
        public bool IgnoreAdler32 { get; set; }
        /*ignore CRC checksums*/
        public bool IgnoreCrc { get; set; }
        /*ignore unknown critical chunks*/
        public bool IgnoreCritical { get; set; }
        /*ignore issues at end of file if possible (missing IEND chunk, too large chunk, ...)*/
        public bool IgnoreEnd { get; set; }
        /*whether to convert the PNG to the color type you want. Default: yes*/
        public bool ColorСonvert { get; set; }

        public bool ReadTextChunks { get; set; }

        public static bool operator ==(PNGDecoderSettings left, PNGDecoderSettings right)
        {
            if (left.IgnoreAdler32 == right.IgnoreAdler32 && left.IgnoreCrc == right.IgnoreCrc
                && left.IgnoreCritical == right.IgnoreCritical && left.ColorСonvert == right.ColorСonvert)
            {
                return true;
            }

            return false;
        }

        public static bool operator !=(PNGDecoderSettings left, PNGDecoderSettings right)
        {
            if (left == right)
            {
                return false;
            }

            return true;
        }
    }

    internal class PNGState
    {
        public PNGState()
        {
            DecoderSettings = new PNGDecoderSettings();
            InfoRaw = new PNGColorMode();
            InfoPng = new PNGInfo();
        }

        public PNGDecoderSettings DecoderSettings { get; set; }

        public PNGColorMode InfoRaw { get; set; }

        public PNGInfo InfoPng { get; set; }

        public uint Error { get; set; }

        public static bool operator ==(PNGState left, PNGState right)
        {
            if (left.DecoderSettings == right.DecoderSettings && left.InfoRaw == right.InfoRaw
                && left.InfoPng == right.InfoPng && left.Error == right.Error)
            {
                return true;
            }

            return false;
        }

        public static bool operator !=(PNGState left, PNGState right)
        {
            if (left == right)
            {
                return false;
            }

            return true;
        }
    }

    public enum InterlaceMethod : byte
    {
        None = 0,
        Adam7 = 1
    }

    internal class PNGInfo
    {
        public PNGInfo()
        {
            ColorMode = new PNGColorMode();
            TextKeys = new List<string>();
            TextStrings = new List<string>();
        }

        /*header (IHDR), palette (PLTE) and transparency (tRNS) chunks*/
        /*compression method of the original file. Always 0.*/
        public byte CompressionMethod { get; set; }
        /*filter method of the original file*/
        public byte FilterMethod { get; set; }
        /*interlace method of the original file: 0=none, 1=Adam7*/
        public InterlaceMethod InterlaceMethod;
        /*color type and bits, palette and transparency of the PNG file*/
        public PNGColorMode ColorMode;

        /*
        Suggested background color chunk (bKGD)
        This uses the same color mode and bit depth as the PNG (except no alpha channel),
        with values truncated to the bit depth in the unsigned integer.
        For grayscale and palette PNGs, the value is stored in background_r. The values
        in background_g and background_b are then unused.
        So when decoding, you may get these in a different color mode than the one you requested
        for the raw pixels.
        When encoding with auto_convert, you must use the color model defined in info_png.color for
        these values. The encoder normally ignores info_png.color when auto_convert is on, but will
        use it to interpret these values (and convert copies of them to its chosen color model).
        When encoding, avoid setting this to an expensive color, such as a non-gray value
        when the image is gray, or the compression will be worse since it will be forced to
        write the PNG with a more expensive color mode (when auto_convert is on).
        The decoder does not use this background color to edit the color of pixels. This is a
        completely optional metadata feature.
        */

        /*is a suggested background color given?*/
        public bool IsBackgroundDefined;
        /*red/gray/palette component of suggested background color*/
        public uint BackgroundR;
        /*green component of suggested background color*/
        public uint BackgroundG;
        /*blue component of suggested background color*/
        public uint BackgroundB;

        /*
        non-international text chunks (tEXt and zTXt)
        The char** arrays each contain num strings. The actual messages are in
        text_strings, while text_keys are keywords that give a short description what
        the actual text represents, e.g. Title, Author, Description, or anything else.
        All the string fields below including keys, names and language tags are null terminated.
        The PNG specification uses null characters for the keys, names and tags, and forbids null
        characters to appear in the main text which is why we can use null termination everywhere here.
        A keyword is minimum 1 character and maximum 79 characters long. It's
        discouraged to use a single line length longer than 79 characters for texts.
        Don't allocate these text buffers yourself. Use the init/cleanup functions
        correctly and use lodepng_add_text and lodepng_clear_text.
        */
        public ulong TextNum; /*the amount of texts in these string buffers (there may be more texts in itext)*/
        public List<string> TextKeys; /*the keyword of a text chunk (e.g. "Comment")*/
        public List<string> TextStrings; /*the actual text*/

        /*
        international text chunks (iTXt)
        Similar to the non-international text chunks, but with additional strings
        "langtags" and "transkeys".
        */

        /*the amount of international texts in this PNG*/
        public ulong ItextNum;
        /*the English keyword of the text chunk (e.g. "Comment")*/
        public string[] ItextKeys;
        /*language tag for this text's language, ISO/IEC 646 string, e.g. ISO 639 language tag*/
        public string[] ItextLangtags;
        /*keyword translated to the international language - UTF-8 string*/
        public string[] ItextTranskeys;
        /*the actual international text - UTF-8 string*/
        public string[] ItextStrings;

        /*time chunk (tIME)*/

        /*set to 1 to make the encoder generate a tIME chunk*/
        public bool IsTimeDefined { get; set; } 

        public tIME Time { get; set; }

        public iTXt InternationalText { get; set; }

        /*phys chunk (pHYs)*/
        /*if 0, there is no pHYs chunk and the values below are undefined, if 1 else there is one*/
        public bool IsPhysDefined; 
        public uint PhysX; /*pixels per unit in x direction*/
        public uint PhysY; /*pixels per unit in y direction*/
        public Unit PhysUnit; /*may be 0 (unknown unit) or 1 (metre)*/

        /*
        Color profile related chunks: gAMA, cHRM, sRGB, iCPP
        LodePNG does not apply any color conversions on pixels in the encoder or decoder and does not interpret these color
        profile values. It merely passes on the information. If you wish to use color profiles and convert colors, please
        use these values with a color management library.
        See the PNG, ICC and sRGB specifications for more information about the meaning of these values.
        */

        /* gAMA chunk: optional, overridden by sRGB or iCCP if those are present. */
        public bool IsGamaDefined; /* Whether a gAMA chunk is present (0 = not present, 1 = present). */
        public uint Gamma;   /* Gamma exponent times 100000 */

        /* cHRM chunk: optional, overridden by sRGB or iCCP if those are present. */
        public bool IsChrmDefined;  /* Whether a cHRM chunk is present (0 = not present, 1 = present). */
        public uint ChrmWhiteX;     /* White Point x times 100000 */
        public uint ChrmWhiteY;     /* White Point y times 100000 */
        public uint ChrmRedX;       /* Red x times 100000 */
        public uint ChrmRedY;       /* Red y times 100000 */
        public uint ChrmGreenX;     /* Green x times 100000 */
        public uint ChrmGreenY;     /* Green y times 100000 */
        public uint ChrmBlueX;      /* Blue x times 100000 */
        public uint ChrmBlueY;      /* Blue y times 100000 */

        public cHRM cHRM { get; set; }

        /*
        sRGB chunk: optional. May not appear at the same time as iCCP.
        If gAMA is also present gAMA must contain value 45455.
        If cHRM is also present cHRM must contain respectively 31270,32900,64000,33000,30000,60000,15000,6000.
        */
        public bool IsSrgbDefined; /* Whether an sRGB chunk is present (0 = not present, 1 = present). */
        public RenderingIntent SrgbIntent;  /* Rendering intent: 0=perceptual, 1=rel. colorimetric, 2=saturation, 3=abs. colorimetric */

        /*
        iCCP chunk: optional. May not appear at the same time as sRGB.
        LodePNG does not parse or use the ICC profile (except its color space header field for an edge case), a
        separate library to handle the ICC data (not included in LodePNG) format is needed to use it for color
        management and conversions.
        For encoding, if iCCP is present, gAMA and cHRM are recommended to be added as well with values that match the ICC
        profile as closely as possible, if you wish to do this you should provide the correct values for gAMA and cHRM and
        enable their '_defined' flags since LodePNG will not automatically compute them from the ICC profile.
        For encoding, the ICC profile is required by the PNG specification to be an "RGB" profile for non-gray
        PNG color types and a "GRAY" profile for gray PNG color types. If you disable auto_convert, you must ensure
        the ICC profile type matches your requested color type, else the encoder gives an error. If auto_convert is
        enabled (the default), and the ICC profile is not a good match for the pixel data, this will result in an encoder
        error if the pixel data has non-gray pixels for a GRAY profile, or a silent less-optimal compression of the pixel
        data if the pixels could be encoded as grayscale but the ICC profile is RGB.
        To avoid this do not set an ICC profile in the image unless there is a good reason for it, and when doing so
        make sure you compute it carefully to avoid the above problems.
        */
        public bool ISIccpDefined;      /* Whether an iCCP chunk is present (0 = not present, 1 = present). */
        public string IccpName;            /* Null terminated string with profile name, 1-79 bytes */

        /*
        The ICC profile in iccp_profile_size bytes.
        Don't allocate this buffer yourself. Use the init/cleanup functions
        correctly and use lodepng_set_icc and lodepng_clear_icc.
        */
        public byte[] IccpProfile;
        public uint IccpProfileSize; /* The size of iccp_profile in bytes */

        /* End of color profile related chunks */


        /*
        unknown chunks: chunks not known by LodePNG, passed on byte for byte.
        There are 3 buffers, one for each position in the PNG where unknown chunks can appear.
        Each buffer contains all unknown chunks for that position consecutively.
        The 3 positions are:
        0: between IHDR and PLTE, 1: between PLTE and IDAT, 2: between IDAT and IEND.
        For encoding, do not store critical chunks or known chunks that are enabled with a "_defined" flag
        above in here, since the encoder will blindly follow this and could then encode an invalid PNG file
        (such as one with two IHDR chunks or the disallowed combination of sRGB with iCCP). But do use
        this if you wish to store an ancillary chunk that is not supported by LodePNG (such as sPLT or hIST),
        or any non-standard PNG chunk.
        Do not allocate or traverse this data yourself. Use the chunk traversing functions declared
        later, such as lodepng_chunk_next and lodepng_chunk_append, to read/write this struct.
        */
        public byte[] UnknownChunksData;

        /*size in bytes of the unknown chunks, given for protection*/
        public ulong[] UnknownChunksSize; 

        public static bool operator ==(PNGInfo left, PNGInfo right)
        {
            if (left.CompressionMethod == right.CompressionMethod && left.FilterMethod == right.FilterMethod
                && left.InterlaceMethod == right.InterlaceMethod && left.ColorMode == right.ColorMode)
            {
                return true;
            }

            return false;
        }

        public static bool operator !=(PNGInfo left, PNGInfo right)
        {
            if (left == right)
            {
                return false;
            }

            return true;
        }
    }

    internal struct PNGTime
    {
        public ushort year;  /*2 bytes used (0-65535)*/
        public byte month;   /*1-12*/
        public byte day;     /*1-31*/
        public byte hour;    /*0-23*/
        public byte minute;  /*0-59*/
        public byte second;  /*0-60 (to allow for leap seconds)*/
    }
}
