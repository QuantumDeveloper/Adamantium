using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.GIF
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
