using System.Collections.Generic;
using Adamantium.Imaging.Bmp;
using Adamantium.Imaging.Dds;
using Adamantium.Imaging.Gif;
using Adamantium.Imaging.Ico;
using Adamantium.Imaging.Jpeg;
using Adamantium.Imaging.Jpeg.Decoder;
using Adamantium.Imaging.Png;
using Adamantium.Imaging.Png.Chunks;
using Adamantium.Imaging.Tga;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging;

public static class ImageExtension
{
    public static IRawBitmap ConvertToRawBitmap(this Image image, ImageFileType fileType = ImageFileType.Png)
    {
        switch (fileType)
        {
            case ImageFileType.Bmp:
            {
                var img = new BmpImage(image.Description);
                img.PixelData = image.PixelBuffer[0].GetPixels<byte>();
                return img;
            }
            case ImageFileType.Jpg:
            {
                var img = new JpegImage();
                img.Width = image.Description.Width;
                img.Height = image.Description.Height;
                img.PixelFormat = image.Description.Format;
                var frame = new JpegFrame();
                var pxBuffer = image.PixelBuffer[0];
                frame.PixelData = pxBuffer.GetPixels<byte>();
                frame.Width = (ushort)pxBuffer.Width;
                frame.Height = (ushort)pxBuffer.Height;
                img.AddFrame(frame);
                return img;
            }
            case ImageFileType.Png:
            {
                var img = new PngImage();
                img.Header = new IHDR();
                img.Header.Width = (int)image.Description.Width;
                img.Header.Height = (int)image.Description.Height;
                var bitDepth = image.Description.Format.SizeOfInBits();
                for (var index = 0; index < image.PixelBuffer.Count; index++)
                {
                    var pixelBuffer = image.PixelBuffer[index];
                    var frame = new PngFrame(pixelBuffer.GetPixels<byte>(), pixelBuffer.Width, pixelBuffer.Height,
                        bitDepth);
                    frame.SequenceNumberFCTL = (uint)index;
                    if (image.pixelBuffers.Length > 1 && index > 0)
                    {
                        img.Frames.Add(frame);
                    }
                    else
                    {
                        img.DefaultImage = frame;
                    }
                }

                return img;
            }
            case ImageFileType.Dds:
            {
                var img = new DdsImage(image.Description);
                var frameDataList = new List<FrameData>();
                for (int i = 0; i < image.PixelBuffer.Count; i++)
                {
                    var pxBuffer = image.PixelBuffer[i];
                    var description = new ImageDescription()
                    {
                        Width = pxBuffer.Width,
                        Height = pxBuffer.Height,
                        Depth = 1,
                        ArraySize = 1,
                        Format = pxBuffer.Format,
                        Dimension = image.Description.Dimension,
                        MipLevels = pxBuffer.MipLevel
                    };

                    var frameData = new FrameData(pxBuffer.GetPixels<byte>(), description, pxBuffer.MipLevel);
                    frameDataList.Add(frameData);
                }

                return img;
            }
            case ImageFileType.Ico:
            {
                var img = new IcoImage(image.Description);
                for (int i = 0; i < image.PixelBuffer.Count; i++)
                {
                    var pxBuffer = image.PixelBuffer[i];
                    var frame = new FrameData(pxBuffer.GetPixels<byte>(), pxBuffer.GetDescription(), pxBuffer.MipLevel);
                    img.AddMipLevel(frame);
                }

                return img;
            }
            case ImageFileType.Gif:
            {
                var img = new GifImage();
                img.Descriptor = new ScreenDescriptor();
                img.Descriptor.Width = (ushort)image.Description.Width;
                img.Descriptor.Height = (ushort)image.Description.Height;
                img.ColorDepth = (byte)image.Description.Format.SizeOfInBits();

                for (var index = 0; index < image.PixelBuffer.Count; index++)
                {
                    var pixelBuffer = image.PixelBuffer[index];
                    var gifFrame = new GifFrame(index);
                    gifFrame.RawPixels = pixelBuffer.GetPixels<byte>();
                    img.AddFrame(gifFrame);
                }

                return img;
            }
            case ImageFileType.Tga:
            {
                var img = new TgaImage(image.Description.Width, image.Description.Height, image.Description.Format);
                img.PixelBuffer = image.PixelBuffer[0].GetPixels<byte>();
                return img;
            }
            case ImageFileType.Tiff:
            {
                break;
            }
        }

        return null;
    }
}