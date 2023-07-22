namespace Adamantium.Imaging.Gif
{
    public class ScreenDescriptor
    {
        public ushort   Width { get; set; }
        public ushort   Height { get; set; }
        public byte     Fields { get; set; }
        public byte     BackgroundColorIndex { get; set; }
        public byte     PixelAspectRatio { get; set; }
    }
}
