using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Gif
{
    public static class GIFHelper
    {
        public static unsafe Image LoadFromMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            var stream = new UnmanagedMemoryStream((byte*)pSource, (long)size);

            GifDecoder decoder = new GifDecoder();
            var img = decoder.Decode(stream);
            return img;
        }

        public static unsafe void SaveToStream(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            GifEncoder encoder = new GifEncoder();
            encoder.Encode(img, imageStream);

            //var quant = new NeuralColorQuantizer();
            //var quant = new DistinctSelectionQuantizer();

            //var result = ImageBuffer.QuantizeImage(pixelBuffers[0], quant, null, 256, true, 1);
            //var img2 = Image.New2D(description.Width, description.Height, new MipMapCount(1), SurfaceFormat.R8G8B8.UNorm);
            //Utilities.CopyMemory(img2.DataPointer, result.DataPointer, result.BufferStride);
            //img2?.Save(imageStream, ImageFileType.Png);

            //var colors1 = pixelBuffers[0].GetPixels<ColorRGB>();
            //var colors2 = result.GetPixels<ColorRGB>();
            //var dist1 = colors1.Distinct().ToArray();
            //var dist2 = colors2.Distinct().ToArray();
        }
    }
}
