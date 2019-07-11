using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Tga
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TGAHeader
    {
        public byte IDLength;
        public byte ColorMapType;
        public byte ImageType;
        public ushort ColorMapFirst;
        public ushort ColorMapLength;
        public byte ColorMapSize;
        public ushort XOrigin;
        public ushort YOrigin;
        public ushort Width;
        public ushort Height;
        public byte BitsPerPixel;
        public byte Descriptor;
    }
}