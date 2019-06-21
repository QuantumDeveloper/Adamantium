using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GifImageDescriptor
    {
        public ushort offsetLeft;
        public ushort offsetTop;
        public ushort width;
        public ushort height;
        public byte fields;
    }
}
