using System.IO;
using Adamantium.Imaging.Jpeg;
using Adamantium.Vulkan.Core;

namespace Adamantium.Imaging;

public static class RawBitmapExtension
{
    public static void Save(this IRawBitmap bitmap, string path, ImageFileType fileType)
    {
        BitmapLoader.Save(bitmap, path, fileType);
    }
    
    public static void Save(this IRawBitmap bitmap, Stream stream, ImageFileType fileType)
    {
        BitmapLoader.Save(bitmap, stream, fileType);
    }
    
    public static ComponentsBuffer ToComponentsBuffer(this IRawBitmap bitmap, ComponentBufferType bufferType)
    {
        if (bufferType == ComponentBufferType.Jpg)
        {
            var raster = GetComponentArrayFromBuffer(bitmap, bufferType);
            var colorModel = new ColorModel() { Colorspace = ColorSpace.RGB, Opaque = true };
            return new ComponentsBuffer(colorModel, raster);
        }
        return null;
    }

    private static byte[][,] GetComponentArrayFromBuffer(IRawBitmap bitmap, ComponentBufferType bufferType)
    {
        var pixelSize = bitmap.GetImageDescription().Format.SizeOfInBytes();
        // How many bytes ONE pixel occupies in the buffer - which is not the same number as how many components we are
        // about to emit, and conflating the two is what made this throw. A 32-bit source written as JPG emits three
        // components and must still step four bytes; a genuine 24-bit source emits three and steps three. The loops used
        // to step one extra byte unconditionally, so a real 24-bit bitmap walked off the end of its own pixels.
        var sourceStride = pixelSize;
        byte[][,] componentsArray;
        if (bufferType == ComponentBufferType.Jpg && pixelSize > 3)
        {
            componentsArray = new byte[3][,];
            pixelSize = 3;
        }
        else
        {
            componentsArray = new byte[pixelSize][,];
        }

        var colors = bitmap.GetRawPixels(0);
        int counter = 0;
        if (pixelSize == 1)
        {
            var redChannel = new byte[bitmap.Width, bitmap.Height];
            for (int i = 0; i < bitmap.Height; ++i)
            {
                for (int k = 0; k < bitmap.Width; ++k)
                {
                    redChannel[k, i] = colors[counter];
                    counter += sourceStride;
                }
            }

            componentsArray[0] = redChannel;
        }
        else if (pixelSize == 2)
        {
            var redChannel = new byte[bitmap.Width, bitmap.Height];
            var greenChannel = new byte[bitmap.Width, bitmap.Height];
            for (int i = 0; i < bitmap.Height; ++i)
            {
                for (int k = 0; k < bitmap.Width; ++k)
                {
                    // [x, y] like every other branch: the arrays are [Width, Height], so indexing them [y, x] threw on
                    // any bitmap that was not square.
                    redChannel[k, i] = colors[counter];
                    greenChannel[k, i] = colors[counter + 1];
                    counter += sourceStride;
                }
            }

            componentsArray[0] = redChannel;
            componentsArray[1] = greenChannel;
        }
        else if (pixelSize == 3)
        {
            var redChannel = new byte[bitmap.Width, bitmap.Height];
            var greenChannel = new byte[bitmap.Width, bitmap.Height];
            var blueChannel = new byte[bitmap.Width, bitmap.Height];
            for (int i = 0; i < bitmap.Height; ++i)
            {
                for (int k = 0; k < bitmap.Width; ++k)
                {
                    redChannel[k, i] = colors[counter];
                    greenChannel[k, i] = colors[counter + 1];
                    blueChannel[k, i] = colors[counter + 2];
                    counter += sourceStride;
                }
            }

            componentsArray[0] = redChannel;
            componentsArray[1] = greenChannel;
            componentsArray[2] = blueChannel;
        }
        else if (pixelSize == 4)
        {
            var redChannel = new byte[bitmap.Width, bitmap.Height];
            var greenChannel = new byte[bitmap.Width, bitmap.Height];
            var blueChannel = new byte[bitmap.Width, bitmap.Height];
            var alphaChannel = new byte[bitmap.Width, bitmap.Height];
            for (int i = 0; i < bitmap.Height; ++i)
            {
                for (int k = 0; k < bitmap.Width; ++k)
                {
                    redChannel[k, i] = colors[counter];
                    greenChannel[k, i] = colors[counter + 1];
                    blueChannel[k, i] = colors[counter + 2];
                    alphaChannel[k, i] = colors[counter + 3];
                    counter += sourceStride;
                }
            }

            componentsArray[0] = redChannel;
            componentsArray[1] = greenChannel;
            componentsArray[2] = blueChannel;
            if (bufferType != ComponentBufferType.Jpg)
            {
                componentsArray[3] = alphaChannel;
            }
        }

        return componentsArray;
    }
}