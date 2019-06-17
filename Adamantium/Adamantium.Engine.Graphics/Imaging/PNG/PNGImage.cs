using Adamantium.Engine.Graphics.Imaging.PNG.Chunks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Engine.Graphics.Imaging.PNG
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

        public PNGFrame DefaultImage => Frames.FirstOrDefault(x => !x.IsPartOfAnimation);

        public bool IsMultiFrame => Frames.Count > 1;

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
                var frame = new PNGFrame(colors, (uint)pixelBuffer.Width, (uint)pixelBuffer.Height, pixelBuffer.PixelSize * 8);
                pngImage.Frames.Add(frame);
            }

            return pngImage;
        }
    }
}
