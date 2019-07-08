using Adamantium.Core;
using Adamantium.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Helpers;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Quantizers;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Quantizers.DistinctSelection;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Quantizers.OptimalPalette;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Quantizers.XiaolinWu;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;

namespace Adamantium.Engine.Graphics.Imaging.GIF
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
            var quantizerResults = new List<QuantizerResult>();
            //var quant = new NeuralColorQuantizer();
            //var quant = new OptimalPaletteQuantizer();
            //var quant = new WuColorQuantizer();
            var quant = new DistinctSelectionQuantizer();
            foreach (PixelBuffer pixelBuffer in img.PixelBuffer)
            {
                //var pixels = pixelBuffer.GetPixels<Color>();
                //var colors = pixels.Distinct().ToList();
                //if (colors.Count <= 256)
                //{
                //    if (pixelBuffer.PixelSize == 3)
                //    {
                //        quantizedImages.Add(pixelBuffer);
                //    }
                //    else if (pixelBuffer.PixelSize == 4)
                //    {
                //        var size = pixelBuffer.Width * pixelBuffer.Height * 3;
                //        var ptr = Utilities.AllocateMemory(size);
                //        var buffer = new PixelBuffer(pixelBuffer.Width, pixelBuffer.Height, Format.R8G8B8_UNORM, pixelBuffer.Width * 3, pixelBuffer.Width * 3 * pixelBuffer.Height, ptr);
                //        var bytes = new byte[size];
                //        int bufOffset = 0;
                //        for (int i = 0; i < pixelBuffer.Width * pixelBuffer.Height; i++)
                //        {
                //            bytes[bufOffset + 0] = pixels[i].R;
                //            bytes[bufOffset + 1] = pixels[i].G;
                //            bytes[bufOffset + 2] = pixels[i].B;
                //            bufOffset += 3;
                //        }

                //        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                //        Utilities.CopyMemory(ptr, handle.AddrOfPinnedObject(), bytes.Length);
                //        handle.Free();

                //        quantizedImages.Add(buffer);
                //    }

                //    ct = new List<Color>();
                //    for (int i = 0; i < colors.Count; ++i)
                //    {
                //        ct.Add(new Color(colors[i].R, colors[i].G, colors[i].B));
                //    }
                //    if (ct.Count < 256)
                //    {
                //        var diff = 256 - ct.Count;
                //        for (int i = 0; i < diff; ++i)
                //        {
                //            ct.Add(new Color());
                //        }
                //    }
                //}
                //else
                {
                    var result = ImageBuffer.QuantizeImage(pixelBuffer, quant, null, 256, true, 1);
                    if (result.ColorTable.Length < 256)
                    {
                        var diff = 256 - result.ColorTable.Length;
                        var lst = result.ColorTable.ToList();
                        for (int i = 0; i < diff; ++i)
                        {
                            lst.Add(new Color());
                        }

                        result.ColorTable = lst.ToArray();
                    }
                    quantizerResults.Add(result);
                }
            }

            for (var k = 0; k < quantizerResults.Count; k++)
            {
                var result = quantizerResults[k];
                var pixels = result.Image.GetPixels<ColorRGB>();

                int index = 0;
                //var colorTable = pixels.Distinct().ToDictionary(x => x, x => index++);
                var colorTableDict = result.ColorTable.Distinct().ToDictionary(x => x, x => index++);
                var imageSize = result.Image.Width * result.Image.Height;
                var indices = new int[imageSize];
                for (int i = 0; i < imageSize; ++i)
                {
                    indices[i] = colorTableDict[new Color(pixels[i].R, pixels[i].G, pixels[i].B)];
                    //indices[i] = ct[pixels[i]];
                }

                var compressedStream = lzw.Compress(indices, 8);

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

                WriteCompressedImage(stream, compressedStream, 8);
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
                var data = compressedData.Skip(offset).Take(blockSize).ToArray();
                //stream.WriteBytes(compressedData[offset.. offset + blockSize]);
                stream.WriteBytes(data);

                offset += blockSize;
            }

            stream.WriteByte(0);
        }
    }
}
