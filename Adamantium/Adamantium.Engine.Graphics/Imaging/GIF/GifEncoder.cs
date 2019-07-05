using Adamantium.Core;
using Adamantium.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Helpers;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Quantizers;

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
            var quantizedImages = new List<PixelBuffer>();
            foreach(PixelBuffer pixelBuffer in img.PixelBuffer)
            {
                var quant = new NeuralColorQuantizer();
                var result = ImageBuffer.QuantizeImage(pixelBuffer, quant, null, 256);
                quantizedImages.Add(result);
            }
        }
    }
}
