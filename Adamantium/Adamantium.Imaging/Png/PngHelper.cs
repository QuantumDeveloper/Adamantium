using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Imaging.Png.IO;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.Png
{
    public static class PngHelper
    {
        // TODO: Rework to support Image class
        public static unsafe Image LoadFromMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            var stream = new PNGStreamReader(pSource, size);
            var decoder = new PngDecoder(stream);
            var img = decoder.Decode();
            return null;
        }

        public static void SaveToStream(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            PngColorType colorType;

            var colorFormat = description.Format.SizeOfInBytes();
            switch (colorFormat)
            {
                case 1:
                    colorType = PngColorType.Grey;
                    break;
                case 4:
                    colorType = PngColorType.RGBA;
                    break;
                default:
                    colorType = PngColorType.RGBA;
                    break;
            }

            PngEncoder encoder = new PngEncoder(imageStream);
            PngState state = new PngState();
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
            state.InfoPng.RepeatCount = img.Frames.NumberOfReplays;
            state.InfoPng.ColorMode.ColorType = colorType;
            state.InfoPng.ColorMode.BitDepth = (uint)description.Format.SizeOfInBits() / (uint)description.Format.SizeOfInBytes();

            PngImage pngImage = PngImage.FromImage(img);

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
