namespace Adamantium.Imaging.Png
{
    internal class PngState
    {
        public PngState()
        {
            DecoderSettings = new PngDecoderSettings();
            EncoderSettings = new PngEncoderSettings();
            ColorModeRaw = new PngColorMode();
            InfoPng = new PngInfo();
        }

        public PngDecoderSettings DecoderSettings { get; set; }

        public PngEncoderSettings EncoderSettings { get; set; }

        public PngColorMode ColorModeRaw { get; set; }

        public PngInfo InfoPng { get; set; }

        public uint Error { get; set; }

        public static bool operator ==(PngState left, PngState right)
        {
            if (left.DecoderSettings == right.DecoderSettings && left.ColorModeRaw == right.ColorModeRaw
                && left.InfoPng == right.InfoPng && left.Error == right.Error)
            {
                return true;
            }

            return false;
        }

        public static bool operator !=(PngState left, PngState right)
        {
            if (left == right)
            {
                return false;
            }

            return true;
        }
    }
}
