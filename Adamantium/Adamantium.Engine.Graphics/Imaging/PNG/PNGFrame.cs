namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGFrame
    {
        public PNGFrame(byte[] pixels, int width, int height, int bitDepth)
        {
            RawPixelBuffer = pixels;
            Width = width;
            Height = height;
            BitDepth = bitDepth;
        }

        public int Width { get; set; }

        public int Height { get; set; }

        public int BitDepth { get; set; }

        public byte[] RawPixelBuffer { get; }

        public byte[] CompressedPixelBuffer { get; set; }
    }
}
