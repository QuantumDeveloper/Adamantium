using Adamantium.Engine.Graphics.Imaging.JPEG.Decoder;
using Adamantium.Engine.Graphics.Imaging.JPEG.Encoder;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics
{
    public static class JPEGHelper
    {
        public static unsafe Image LoadFromJpegMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            Image image = null;
            using (var stream = new UnmanagedMemoryStream((byte*)pSource, size))
            {
                var decoder = new JpegDecoder(stream);
                image = decoder.Decode();
            }

            return image;
        }

        public static void SaveToJpegMemory(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            JpegEncoder encoder = new JpegEncoder(pixelBuffers[0], 100, imageStream);
            encoder.Encode();
        }

    }
}
