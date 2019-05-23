using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public static class PNGHelper
    {
        internal static byte[] PngHeader = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static unsafe Image LoadFromPNGMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            var stream = new UnmanagedMemoryStream((byte*)pSource, size);
            PNGDecoder decoder = new PNGDecoder(stream);
            decoder.Decode();
            return null;
        }

        public static void SavePngToMemory(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {

        }
    }
}
