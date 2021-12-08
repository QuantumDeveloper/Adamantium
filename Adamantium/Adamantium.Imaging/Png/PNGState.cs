namespace Adamantium.Imaging.Png
{
    internal class PNGState
    {
        public PNGState()
        {
            DecoderSettings = new PNGDecoderSettings();
            EncoderSettings = new PNGEncoderSettings();
            ColorModeRaw = new PNGColorMode();
            InfoPng = new PNGInfo();
        }

        public PNGDecoderSettings DecoderSettings { get; set; }

        public PNGEncoderSettings EncoderSettings { get; set; }

        public PNGColorMode ColorModeRaw { get; set; }

        public PNGInfo InfoPng { get; set; }

        public uint Error { get; set; }

        public static bool operator ==(PNGState left, PNGState right)
        {
            if (left.DecoderSettings == right.DecoderSettings && left.ColorModeRaw == right.ColorModeRaw
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
}
