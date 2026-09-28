using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Gif
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PlainTextExtension
    {
        public ushort   Left { get; set; }
        public ushort   Top { get; set; }
        public ushort   Width { get; set; }
        public ushort   Height { get; set; }
        public byte     CellWidth { get; set; }
        public byte     CellHeight { get; set; }
        public byte     ForegroundColor { get; set; }
        public byte     BckgroundColor { get; set; }
    }
}
