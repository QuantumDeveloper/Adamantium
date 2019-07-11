using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Ico
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct IconDir
    {
        public ushort Reserved;   // Reserved
        public ushort Type;       // resource type (1 for icons)
        public ushort Count;      // how many images?
    }
}