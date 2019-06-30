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

        public static void SavePngToMemory(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            PNGColorType colorType = PNGColorType.RGB;
            if (description.Format.SizeOfInBytes() == 4)
            {
                colorType = PNGColorType.RGBA;
            }

            PNGEncoder encoder = new PNGEncoder(imageStream);
            PNGState state = new PNGState();
            state.EncoderSettings.BType = 2;
            state.EncoderSettings.UseLZ77 = true;
            state.InfoPng.InterlaceMethod = InterlaceMethod.None;
            state.EncoderSettings.FilterStrategy = FilterStrategy.MinSum;
            state.InfoRaw.ColorType = colorType;
            state.InfoRaw.BitDepth = (uint)description.Format.SizeOfInBits() / (uint)description.Format.SizeOfInBytes();

            state.InfoPng.FramesCount = (uint)count;
            if (img.DefaultImage != null)
            {
                state.InfoPng.FramesCount--;
            }
            state.InfoPng.RepeatCount = img.NumberOfReplays;
            state.InfoPng.ColorMode.ColorType = colorType;
            state.InfoPng.ColorMode.BitDepth = (uint)description.Format.SizeOfInBits() / (uint)description.Format.SizeOfInBytes();

            PNGImage pngImage = PNGImage.FromImage(img);

            encoder.Encode(pngImage, state);

            //var images = new PNGImage[pngImage.Frames.Count];
            //for (int i = 0; i < images.Length; ++i)
            //{
            //    var image = new PNGImage();
            //    image.Frames.Add(pngImage.Frames[i]);
            //    image.Header = new Chunks.IHDR();
            //    image.Header.Width = (int)image.Frames[0].Width;
            //    image.Header.Height = (int)image.Frames[0].Height;
            //    images[i] = image;
            //    var enc = new PNGEncoder();
            //    enc.Encode(images[i], state);
            //    var frameBytes = enc.GetAllBytes();
            //    File.WriteAllBytes($"{i}.png",frameBytes);
            //}
        }
    }
}
