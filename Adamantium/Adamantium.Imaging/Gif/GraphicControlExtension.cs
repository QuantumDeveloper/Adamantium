namespace Adamantium.Imaging.Gif
{
    internal class GraphicControlExtension
    {
        public byte Fields { get; set; }
        public ushort DelayTime { get; set; }
        public byte TransparentColorIndex { get; set; }
        public DisposalMethod DisposalMethod { get; set; }
        public bool TransparencyAvailable { get; set; }
    }
}
