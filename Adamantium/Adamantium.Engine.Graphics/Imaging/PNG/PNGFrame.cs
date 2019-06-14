using Adamantium.Engine.Graphics.Imaging.PNG.Chunks;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGFrame
    {
        public PNGFrame()
        {
            
        }

        public PNGFrame(byte[] pixels, int width, int height, int bitDepth)
        {
            RawPixelBuffer = pixels;
            Width = width;
            Height = height;
            BitDepth = bitDepth;
        }

        public bool IsPartOfAnimation { get; set; }

        public fcTL FrameControl { get; set; }

        public uint SequenceNumber { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int BitDepth { get; set; }

        public byte[] RawPixelBuffer { get; set; }

        public byte[] FrameData { get; set; }
    }
}
