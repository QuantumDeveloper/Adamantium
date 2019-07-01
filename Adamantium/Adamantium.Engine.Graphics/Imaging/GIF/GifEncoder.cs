using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    public class GifEncoder
    {
        private const string GIFHeader = "GIF89a";
        private const string NetscapeAppExt = "Netscape2.0";

        private Stream stream;
        private Image image;

        public void Encode(Image img, Stream stream)
        {
            this.stream = stream;
            image = img;

            WriteGifHeader();
            WriteHeader(ref img.Description);
            WriteApplicationExtension();
        }

        private void WriteGifHeader()
        {
            var header = Encoding.ASCII.GetBytes(GIFHeader);
            stream.WriteBytes(header);
        }

        private void WriteHeader(ref ImageDescription description)
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

        private void WriteApplicationExtension()
        {
            stream.WriteByte((byte)GifChunkCodes.ExtensionIntroducer);
            stream.WriteByte(11);
            stream.WriteByte((byte)GifChunkCodes.ApplicationExtension);
            stream.WriteBytes(Encoding.ASCII.GetBytes(NetscapeAppExt));

            stream.WriteByte(0); // Trailer
        }

        private void WriteGraphicsExtension()
        {
            stream.WriteByte((byte)GifChunkCodes.GraphicControl);
            stream.WriteByte(0);
            stream.WriteUInt16(0);
            stream.WriteByte(0);
            stream.WriteByte(0);

            stream.WriteByte(0); // Trailer
        }

        private void WriteImageData()
        {

        }
    }
}
