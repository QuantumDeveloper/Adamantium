using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GifExtension
    {
        public GifChunkCodes extensionCode;
        public byte blockSize;
    }
}
