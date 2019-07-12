﻿using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Imaging.PaletteQuantizer;
using Adamantium.Imaging.PaletteQuantizer.Helpers;
using Adamantium.Imaging.PaletteQuantizer.Quantizers.DistinctSelection;
using Adamantium.Mathematics;

namespace Adamantium.Imaging.Gif
{
    public class GifEncoder
    {
        private const string GIFHeader = "GIF89a";
        private const string NetscapeAppExt = "NETSCAPE2.0";

        private LZW lzw;

        public GifEncoder()
        {
            lzw = new LZW();
        }

        public void Encode(Image img, Stream stream)
        {
            WriteGifHeader(stream);
            WriteHeader(ref img.Description, stream);
            //WriteGraphicsControlExtension(stream, 0, 0);
            WriteApplicationExtension(stream, 0);
            WriteImageData(img, stream);
            stream.WriteByte((byte)GifChunkCodes.Trailer);
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
            // 8 bits depth
            //fields |= (1 << 4);
            //fields |= (1 << 5);
            //fields |= (1 << 6);
            fields = 112;

            //var colorDepth = (((fields >> 4) & 7)+1);

            //fields = 247;
            stream.WriteByte(fields);
            stream.WriteByte(0);
            stream.WriteByte(0);
        }

        private void WriteApplicationExtension(Stream stream, ushort loopCount)
        {
            stream.WriteByte((byte)GifChunkCodes.ExtensionIntroducer);

            stream.WriteByte((byte)GifChunkCodes.ApplicationExtension);
            stream.WriteByte(11);

            stream.WriteBytes(Encoding.ASCII.GetBytes(NetscapeAppExt));
            stream.WriteByte(3); // blockSize (Always 3)
            stream.WriteByte(1); //Always 1 (Don`t know what is it)
            stream.WriteUInt16(loopCount); // Number of animation loops

            stream.WriteByte(0); // Trailer
        }

        private void WriteGraphicsControlExtension(Stream stream, ushort delay, int transparentIndex, DisposalMethod disposalMethod)
        {
            stream.WriteByte((byte)GifChunkCodes.ExtensionIntroducer);

            stream.WriteByte((byte)GifChunkCodes.GraphicControl);
            stream.WriteByte(5);

            stream.WriteByte(0);
            stream.WriteUInt16(0);
            stream.WriteByte(0);
            stream.WriteByte((byte)disposalMethod);

            stream.WriteByte(0); // Trailer
        }

        private void WriteImageData(Image img, Stream stream)
        {
            var quantizerResults = new QuantizerResult[img.PixelBuffer.Count];
            Parallel.For(
                0,
                img.PixelBuffer.Count,
                index =>
                {
                    var quant = new DistinctSelectionQuantizer();
                    var result = ImageBuffer.QuantizeImage(img.PixelBuffer[index], quant, null, 256, true, 1);

                    if (result.ColorTable.Length < 256)
                    {
                        var diff = 256 - result.ColorTable.Length;
                        var lst = result.ColorTable.ToList();
                        for (int k = 0; k < diff; ++k)
                        {
                            lst.Add(new Color());
                        }

                        result.ColorTable = lst.ToArray();
                    }
                    result.CompressedPixels = lzw.Compress(result.IndexTable, 8);
                    quantizerResults[index] = result;
                }
            );
            for (var k = 0; k < quantizerResults.Length; k++)
            {
                var result = quantizerResults[k];
                byte fields = 0;
                //fields |= (1 << 0); // LCT presence flag
                //// 256 colors
                //fields |= (1 << 5);
                //fields |= (1 << 6);
                //fields |= (1 << 7);

                fields = 135;

                //var s = 1 << ((fields & 0x07) + 1);
                //var present = fields & 0x80;

                stream.WriteByte((byte) GifChunkCodes.ImageDescriptor);
                stream.WriteUInt16(0);
                stream.WriteUInt16(0);
                stream.WriteUInt16((ushort) result.Image.Width);
                stream.WriteUInt16((ushort) result.Image.Height);
                stream.WriteByte(fields);

                //Writing local color table
                foreach (var color in result.ColorTable)
                {
                    stream.WriteBytes(new[] {color.R, color.G, color.B});
                }

                WriteCompressedImage(stream, result.CompressedPixels, 8);
                WriteGraphicsControlExtension(stream, 0, 0, DisposalMethod.None);
            }
        }

        private void WriteCompressedImage(Stream stream, byte[] compressedData, byte colorDepth)
        {
            stream.WriteByte(colorDepth);

            byte blockSize;
            int offset = 0;
            int dataSize = compressedData.Length;
            while (offset < dataSize)
            {
                if (offset == dataSize) break;

                blockSize = dataSize - offset > 255 ? byte.MaxValue : (byte)(dataSize - offset);
                stream.WriteByte(blockSize);
                for(int i = 0; i<blockSize; ++i)
                {
                    stream.WriteByte(compressedData[offset]);
                    offset++;
                }
            }

            stream.WriteByte(0);
        }
    }
}