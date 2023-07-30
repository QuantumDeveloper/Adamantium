using System;
using System.IO;

namespace Adamantium.Imaging.Tga
{
    internal class TgaHelper
    {
        public static TgaImage LoadFromMemory(IntPtr source, long size)
        {
            TgaConversionFlags conversionFlags = 0;
            ImageDescription description;
            var result = TgaDecoder.DecodeTgaHeader(source, size, out description, out var offset, out conversionFlags);

            if (result == false)
            {
                return null;
            }

            if (offset > size)
            {
                return null;
            }

            var image = TgaDecoder.CreateImageFromTGA(source, offset, description, conversionFlags);

            return image;
        }

        public static void SaveToStream(IRawBitmap bitmap, Stream imageStream)
        {
            TgaEncoder.SaveToTgaStream(bitmap, imageStream);
        }
    }
}
