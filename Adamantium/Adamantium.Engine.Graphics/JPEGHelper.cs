using Adamantium.Engine.Graphics.Imaging;
using Adamantium.Engine.Graphics.Imaging.JPEG.Decoder;
using Adamantium.Engine.Graphics.Imaging.JPEG.Encoder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public static class JPEGHelper
    {
        public static unsafe Image LoadFromJpegMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            using (var stream = new UnmanagedMemoryStream((byte*)pSource, size))
            {
                var decoder = new JpegDecoder(stream);
                var decodedImage = decoder.Decode();
            }

            return null;
        }

        public static void SaveToJpegMemory(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            //JpegEncoder encoder = new JpegEncoder(pixelBuffers[0], 1, imageStream);
            //encoder.Encode();
        }

    }
}
