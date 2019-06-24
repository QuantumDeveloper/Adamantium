using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PlainTextExtension
    {
        public ushort   left;
        public ushort   top;
        public ushort   width;
        public ushort   height;
        public byte     cellWidth;
        public byte     cellHeight;
        public byte     foregroundColor;
        public byte     bckgroundColor;
    }
}
