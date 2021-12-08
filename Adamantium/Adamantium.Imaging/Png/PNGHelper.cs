using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Imaging.Png.IO;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.Png
{
    public static class PNGHelper
    {
        public static unsafe Image LoadFromMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            var stream = new PNGStreamReader(pSource, size);
            PNGDecoder decoder = new PNGDecoder(stream);
            var img = decoder.Decode();
            return img;
        }

        public static void SaveToStream(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            PNGColorType colorType;

            var colorFormat = description.Format.SizeOfInBytes();
            switch (colorFormat)
            {
                case 1:
                    colorType = PNGColorType.Grey;
                    break;
                case 4:
                    colorType = PNGColorType.RGBA;
                    break;
                default:
                    colorType = PNGColorType.RGBA;
                    break;
            }

            PNGEncoder encoder = new PNGEncoder(imageStream);
            PNGState state = new PNGState();
            state.EncoderSettings.BType = 2;
            state.EncoderSettings.UseLZ77 = true;
            state.InfoPng.InterlaceMethod = InterlaceMethod.None;
            state.EncoderSettings.FilterStrategy = FilterStrategy.MinSum;
            state.EncoderSettings.AutoConvert = true;
            state.ColorModeRaw.ColorType = colorType;
            state.ColorModeRaw.BitDepth = (uint)description.Format.SizeOfInBits() / (uint)description.Format.SizeOfInBytes();

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
