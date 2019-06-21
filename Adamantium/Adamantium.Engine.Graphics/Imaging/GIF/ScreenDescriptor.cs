using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ScreenDescriptor
    {
        public ushort width;
        public ushort height;
        public byte fields;
        public byte backgroundColorIndex;
        public byte pixelAspectRatio;
    }
}
