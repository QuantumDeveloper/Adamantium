using Adamantium.Engine.Graphics.Imaging.PNG.IO;
using AdamantiumVulkan.Core;
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
            var stream = new PNGStreamReader(pSource, size);
            PNGDecoder decoder = new PNGDecoder(stream);
            var img = decoder.Decode();
            return img;
        }

        public static void SavePngToMemory(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            PNGEncoder encoder = new PNGEncoder(imageStream);
            PNGState state = new PNGState();
            state.EncoderSettings.BType = 2;
            state.EncoderSettings.UseLZ77 = false;
            state.InfoPng.InterlaceMethod = InterlaceMethod.None;
            state.EncoderSettings.FilterStrategy = FilterStrategy.MinSum;
            state.InfoRaw.ColorType = PNGColorType.RGBA;
            state.InfoRaw.BitDepth = (uint)description.Format.SizeOfInBits() / (uint)description.Format.SizeOfInBytes();
            //state.InfoRaw.ColorType = PNGColorType.Grey;
            //state.InfoRaw.BitDepth = 8;
            state.InfoPng.ColorMode.ColorType = PNGColorType.RGBA;
            state.InfoPng.ColorMode.BitDepth = (uint)description.Format.SizeOfInBits() / (uint)description.Format.SizeOfInBytes(); ;
            encoder.Encode(pixelBuffers, state);
        }
    }
}
