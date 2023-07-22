namespace Adamantium.Imaging.Png
{
    public class PngDecoderSettings
    {
        public PngDecoderSettings()
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

        public static bool operator ==(PngDecoderSettings left, PngDecoderSettings right)
        {
            if (left.IgnoreAdler32 == right.IgnoreAdler32 && left.IgnoreCrc == right.IgnoreCrc
                && left.IgnoreCritical == right.IgnoreCritical && left.ColorСonvert == right.ColorСonvert)
            {
                return true;
            }

            return false;
        }

        public static bool operator !=(PngDecoderSettings left, PngDecoderSettings right)
        {
            if (left == right)
            {
                return false;
            }

            return true;
        }
    }
}
