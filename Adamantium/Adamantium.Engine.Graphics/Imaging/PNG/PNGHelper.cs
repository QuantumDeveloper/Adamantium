using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public static class PNGHelper
    {
        public static unsafe Image LoadFromPNGMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            var stream = new PNGStream(pSource, size);
            PNGDecoder decoder = new PNGDecoder(stream);
            var img = decoder.Decode();
            return img;
        }

        public static void SavePngToMemory(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {

        }
    }
}
