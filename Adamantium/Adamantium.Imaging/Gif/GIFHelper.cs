﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Core;

namespace Adamantium.Imaging.Gif
{
    public static class GIFHelper
    {
        public static unsafe IRawBitmap LoadFromMemory(IntPtr pSource, long size)
        {
            var stream = new UnmanagedMemoryStream((byte*)pSource, size);

            var decoder = new GifDecoder();
            var gifImage = decoder.Decode(stream);
            return gifImage;
        }

        public static void SaveToStream(IRawBitmap image, Stream imageStream)
        {
            GifEncoder encoder = new GifEncoder();
            encoder.Encode(image, imageStream);

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
