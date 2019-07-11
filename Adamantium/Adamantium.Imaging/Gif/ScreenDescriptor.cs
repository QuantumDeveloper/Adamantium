namespace Adamantium.Imaging.Gif
{
    internal class ScreenDescriptor
    {
        public ushort   Width { get; set; }
        public ushort   Height { get; set; }
        public byte     Fields { get; set; }
        public byte     BackgroundColorIndex { get; set; }
        public byte     PixelAspectRatio { get; set; }
    }
}
