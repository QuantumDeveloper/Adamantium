using System.Runtime.InteropServices;
using Adamantium.Imaging.Bmp;

namespace Adamantium.Imaging.Ico
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IconImageInfo
    {
        public BitmapInfoHeader Header;  // DIB header
        // Color table (short 4 bytes) //RGBQUAD
        public byte R;
        public byte G;
        public byte B;
        public byte reserved;
        public byte XOR;      // DIB bits for XOR mask
        public byte AND;      // DIB bits for AND mask
    }
}