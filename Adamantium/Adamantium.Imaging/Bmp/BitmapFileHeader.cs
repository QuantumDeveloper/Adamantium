using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Bmp
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct BitmapFileHeader
    {
        // File type always BM which is 0x4D42
        //[FieldOffset(0)]
        public ushort fileType;
        // Size of the file (in bytes)
        //[FieldOffset(2)]
        public uint fileSize;
        // Reserved, always 0
        //[FieldOffset(6)]
        public ushort reserved1;
        // Reserved, always 0
        //[FieldOffset(8)]
        public ushort reserved2;
        // Start position of pixel data (bytes from the beginning of the file)
        //[FieldOffset(10)]
        public uint offsetData;
    };
}