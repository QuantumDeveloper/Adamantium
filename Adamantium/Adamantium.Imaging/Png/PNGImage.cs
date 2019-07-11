using System.Collections.Generic;
using Adamantium.Imaging.Png.Chunks;

namespace Adamantium.Imaging.Png
{
    internal class PNGImage
    {
        public PNGImage()
        {
            Frames = new List<PNGFrame>();
        }

        public uint FramesCount { get; set; }

        public uint RepeatCout { get; set; }

        public IHDR Header { get; set; }

        public List<PNGFrame> Frames { get; }

        public PNGFrame DefaultImage { get; set; }

        public bool IsMultiFrame => Frames.Count > 1;

        public static PNGImage FromImage(Image image)
        {
            var png = FromPixelBuffers(image.PixelBuffer);
            png.DefaultImage = GetFrameFromBuffer(image.DefaultImage);
            png.Header = new IHDR();
            png.Header.Width = image.Description.Width;
            png.Header.Height = image.Description.Height;

            return png;
        }

        public static PNGImage FromPixelBuffers(params PixelBuffer[] buffers)
        {
            var pngImage = new PNGImage();
            for (int i = 0; i < buffers.Length; ++i)
            {
                var pixelBuffer = buffers[i];
                var frame = GetFrameFromBuffer(pixelBuffer);
                pngImage.Frames.Add(frame);
            }

            return pngImage;
        }

        private static PNGFrame GetFrameFromBuffer(PixelBuffer pixelBuffer)
        {
            if (pixelBuffer == null)
            {
                return null;
            }

            var pixels = pixelBuffer.GetPixels<byte>();
            var frame = new PNGFrame(pixels, (uint)pixelBuffer.Width, (uint)pixelBuffer.Height, pixelBuffer.PixelSize * 8);
            frame.DelayNumerator = pixelBuffer.DelayNumerator;
            frame.DelayDenominator = pixelBuffer.DelayDenominator;
            frame.XOffset = pixelBuffer.XOffset;
            frame.YOffset = pixelBuffer.YOffset;
            frame.SequenceNumberFCTL = pixelBuffer.SequenceNumber;

            return frame;
        }
    }
}
