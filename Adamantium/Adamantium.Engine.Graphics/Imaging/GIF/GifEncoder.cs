using Adamantium.Core;
using Adamantium.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    public class GifEncoder
    {
        private const string GIFHeader = "GIF89a";
        private const string NetscapeAppExt = "Netscape2.0";

        public void Encode(Image img, Stream stream)
        {
            WriteGifHeader(stream);
            WriteHeader(ref img.Description, stream);
            WriteApplicationExtension(stream);
        }

        private void WriteGifHeader(Stream stream)
        {
            var header = Encoding.ASCII.GetBytes(GIFHeader);
            stream.WriteBytes(header);
        }

        private void WriteHeader(ref ImageDescription description, Stream stream)
        {
            stream.WriteUInt16((ushort)description.Width);
            stream.WriteUInt16((ushort)description.Height);
            byte fields = 0;
            fields |= (1 << 4);
            fields |= (1 << 5);
            fields |= (1 << 6); ;
            stream.WriteByte(fields);
            stream.WriteByte(0);
            stream.WriteByte(0);
        }

        private void WriteApplicationExtension(Stream stream)
        {
            stream.WriteByte((byte)GifChunkCodes.ExtensionIntroducer);
            stream.WriteByte(11);
            stream.WriteByte((byte)GifChunkCodes.ApplicationExtension);
            stream.WriteBytes(Encoding.ASCII.GetBytes(NetscapeAppExt));

            stream.WriteByte(0); // Trailer
        }

        private void WriteGraphicsExtension(Stream stream)
        {
            stream.WriteByte((byte)GifChunkCodes.GraphicControl);
            stream.WriteByte(0);
            stream.WriteUInt16(0);
            stream.WriteByte(0);
            stream.WriteByte(0);

            stream.WriteByte(0); // Trailer
        }

        private void WriteImageData(Image img, Stream stream)
        {
            var descr = img.Description;
            foreach(PixelBuffer pixelBuffer in img.PixelBuffer)
            {
                int[] indexBuffer = new int[descr.Width * descr.Height]; 
                if (pixelBuffer.PixelSize == 4)
                {
                    var colorsRGBA = pixelBuffer.GetPixels<Color>();
                    var colorTable = colorsRGBA.Distinct().ToArray();
                    if (colorTable.Length > 256)
                    {

                    }



                    var tmp = pixelBuffer.GetPixels<byte>();
                    var colors = new byte[descr.Width * descr.Height * 3];
                    int offset = 0;
                    for (int i = 0; i < 0; i += 4)
                    {
                        colors[offset + 0] = tmp[i + 0];
                        colors[offset + 1] = tmp[i + 1];
                        colors[offset + 2] = tmp[i + 2];
                        offset += 3;
                    }
                }
                else
                {
                    var colorsRGB = pixelBuffer.GetPixels<ColorRGB>();
                    var colorTable = colorsRGB.Distinct().ToArray();
                    if (colorTable.Length > 256)
                    {

                    }
                }
            }
        }
    }
}
