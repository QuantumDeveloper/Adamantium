using Adamantium.Core;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{

    public static class GIFHelper
    {
        public static unsafe Image LoadFromGifMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            var stream = new UnmanagedMemoryStream((byte*)pSource, (long)size);

            GifDecoder decoder = new GifDecoder();
            decoder.Decode(stream);
            return null;
        }

        public static unsafe void SaveToGIFStream(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {

        }
    }
}
