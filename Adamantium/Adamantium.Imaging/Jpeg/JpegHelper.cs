﻿using System;
using System.IO;
using Adamantium.Imaging.Jpeg.Decoder;
using Adamantium.Imaging.Jpeg.Encoder;

namespace Adamantium.Imaging.Jpeg
{
    public static class JpegHelper
    {
        public static unsafe JpegImage LoadFromMemory(IntPtr pSource, long size)
        {
            JpegImage image = null;
            using (var stream = new UnmanagedMemoryStream((byte*)pSource, size))
            {
                var decoder = new JpegDecoder(stream);
                image = decoder.Decode();
            }

            return image;
        }

        public static void SaveToStream(IRawBitmap img, Stream imageStream)
        {
            JpegEncoder encoder = new JpegEncoder(img, 100, imageStream);
            encoder.Encode();
        }

    }
}
