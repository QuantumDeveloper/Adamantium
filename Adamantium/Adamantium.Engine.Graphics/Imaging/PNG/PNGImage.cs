using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGImage
    {
        public PNGImage()
        {
            Frames = new List<PNGFrame>();
        }

        public List<PNGFrame> Frames { get; }

        public static PNGImage FromImage(Image image)
        {
            return FromPixelBuffers(image.PixelBuffer);
        }

        public static PNGImage FromPixelBuffers(PixelBuffer[] buffers)
        {
            var pngImage = new PNGImage();
            for (int i = 0; i < buffers.Length; ++i)
            {
                var pixelBuffer = buffers[i];
                var colors = pixelBuffer.GetPixels<byte>();
                var frame = new PNGFrame(colors, pixelBuffer.Width, pixelBuffer.Height, pixelBuffer.PixelSize * 8);
                pngImage.Frames.Add(frame);
            }

            return pngImage;
        }
    }
}
