using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GraphicControlExtension
    {
        public byte fields;
        public ushort delayTime;
        public byte transparentColorIndex;
    }
}
