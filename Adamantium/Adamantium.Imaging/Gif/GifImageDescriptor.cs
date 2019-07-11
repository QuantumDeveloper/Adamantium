namespace Adamantium.Imaging.Gif
{
    internal class GifImageDescriptor
    {
        public ushort OffsetLeft { get; set; }
        public ushort OffsetTop { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public byte Fields { get; set; }
    }
}
