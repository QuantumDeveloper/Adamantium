using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Ico
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IconInfo
    {
        public byte Width;              // Width of the image
        public byte Height;             // Height of the image (times 2)
        public byte ColorCount;         // Number of colors in image (0 if >=8bpp)
        public byte Reserved;           // Reserved
        public ushort Planes;           // Color Planes
        public ushort BitCount;         // Bits per pixel
        public uint BytesInRes;         // how many bytes in this resource?
        public uint ImageOffset;        // where in the file is this image
    }
}