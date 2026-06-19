using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Imaging.Png.IO;
using Adamantium.Vulkan.Core;

namespace Adamantium.Imaging.Png
{
    public static class PngHelper
    {
        // TODO: Rework to support Image class
        public static unsafe IRawBitmap LoadFromMemory(IntPtr pSource, ulong size)
        {
            var stream = new PNGStreamReader(pSource, size);
            var decoder = new PngDecoder(stream);
            var img = decoder.Decode();
            return img;
        }

        public static void SaveToStream(IRawBitmap img, Stream imageStream)
        {
            PngColorType colorType;

            var description = img.GetImageDescription();
            var colorFormat = description.Format.SizeOfInBytes();
            switch (colorFormat)
            {
                case 1:
                    colorType = PngColorType.Grey;
                    break;
                default:
                    colorType = PngColorType.RGBA;
                    break;
            }

            var encoder = new PngEncoder(imageStream);
            var state = new PngState
            {
                EncoderSettings =
                {
                    BType = 2,
                    UseLZ77 = true,
                    FilterStrategy = FilterStrategy.MinSum,
                    AutoConvert = true
                },
                InfoPng =
                {
                    InterlaceMethod = InterlaceMethod.None,
                    FramesCount = img.FramesCount
                },
                ColorModeRaw =
                {
                    ColorType = colorType,
                    BitDepth = (uint)description.Format.SizeOfInBits() / (uint)description.Format.SizeOfInBytes()
                }
            };

            state.InfoPng.FramesCount--;
            state.InfoPng.RepeatCount = img.NumberOfReplays;
            state.InfoPng.ColorMode.ColorType = colorType;
            state.InfoPng.ColorMode.BitDepth = (uint)description.Format.SizeOfInBits() / (uint)description.Format.SizeOfInBytes();

            var pngImage = img is PngImage image ? image : PngImage.FromImage(img);

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
