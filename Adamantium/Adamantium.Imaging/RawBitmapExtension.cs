using System.IO;
using Adamantium.Imaging.Jpeg;
using AdamantiumVulkan.Core;

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
                    redChannel[i, k] = colors[counter];
                    counter++;
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
                    redChannel[i, k] = colors[counter++];
                    greenChannel[i, k] = colors[counter++];
                    counter++;
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
                    redChannel[k, i] = colors[counter++];
                    greenChannel[k, i] = colors[counter++];
                    blueChannel[k, i] = colors[counter++];
                    counter++;
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
                    redChannel[k, i] = colors[counter++];
                    greenChannel[k, i] = colors[counter++];
                    blueChannel[k, i] = colors[counter++];
                    alphaChannel[k, i] = colors[counter++];
                    counter++;
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