using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Imaging.Jpeg.Decoder;
using Adamantium.Imaging.Jpeg.Encoder;

namespace Adamantium.Imaging.Jpeg
{
    internal static class JpegHelper
    {
        public static unsafe Image LoadFromMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            Image image = null;
            using (var stream = new UnmanagedMemoryStream((byte*)pSource, size))
            {
                var decoder = new JpegDecoder(stream);
                image = decoder.Decode();
            }

            return image;
        }

        public static void SaveToStream(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            JpegEncoder encoder = new JpegEncoder(pixelBuffers[0], 100, imageStream);
            encoder.Encode();
        }

    }
}
