using Adamantium.Core;
using Adamantium.Mathematics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    public class GifDecoder
    {
        private const string GIFHeader = "GIF89a";
        private LZW lzw;

        public GifDecoder()
        {
            lzw = new LZW();
        }

        public unsafe Image Decode(UnmanagedMemoryStream stream)
        {
            if (!ReadGIFHeader(stream))
            {
                throw new ArgumentException("Given file is not a GIF file");
            }

            var gifImage = new GifImage();
            ScreenDescriptor screenDescriptor = new ScreenDescriptor();
            screenDescriptor.Width = stream.ReadUInt16();
            screenDescriptor.Height = stream.ReadUInt16();
            screenDescriptor.Fields = (byte)stream.ReadByte();
            screenDescriptor.BackgroundColorIndex = (byte)stream.ReadByte();
            screenDescriptor.PixelAspectRatio = (byte)stream.ReadByte();

            /* Color Space's Depth */
            gifImage.ColorDepth = (byte)(((screenDescriptor.Fields >> 4) & 7) + 1);
            /* Ignore Sort Flag. */
            /* GCT Size */
            var gctSize = 1 << ((screenDescriptor.Fields & 0x07) + 1);

            /* Presence of GCT (global color table) */
            if ((screenDescriptor.Fields & 0x80) != 0)
            {
                var сolorTable = new byte[3 * gctSize];
                stream.Read(сolorTable, 0, сolorTable.Length);
                int offset = 0;
                for (int i = 0; i < gctSize; i++)
                {
                    gifImage.GlobalColorTable.Add(new ColorRGB(сolorTable[offset], сolorTable[offset + 1], сolorTable[offset + 2]));
                    offset += 3;
                }
            }

            gifImage.Descriptor = screenDescriptor;

            GifChunkCodes blockType = GifChunkCodes.None;
            while (blockType != GifChunkCodes.Trailer)
            {
                blockType = (GifChunkCodes)stream.ReadByte();

                switch (blockType)
                {
                    case GifChunkCodes.ImageDescriptor:
                        ProcessImageDescriptor(stream, gifImage);
                        break;
                    case GifChunkCodes.ExtensionIntroducer:
                        ProcessExtension(stream, gifImage);
                        break;
                    case GifChunkCodes.Trailer:
                        break;
                }
            }

            return DecodeAllFrames(gifImage);
        }

        private Image DecodeAllFrames(GifImage gif)
        {
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frame = gif.Frames[i];
                frame.IndexData = lzw.Decompress(frame.CompressedData.ToArray(), frame.LzwMinimumCodeSize);
                GetImageFromIndexStream(frame, i, gif);
            }

            ImageDescription description = new ImageDescription();
            description.Width = gif.Descriptor.Width;
            description.Height = gif.Descriptor.Height;
            description.MipLevels = 1;
            description.Dimension = TextureDimension.Texture2D;
            description.Format = AdamantiumVulkan.Core.Format.R8G8B8_UNORM;
            description.ArraySize = 1;
            description.Depth = 1;

            var img = Image.New(description);
            //for (int i = 0; i < gif.Frames.Count; i++)
            //{
            //    GifFrame frame = gif.Frames[i];
            //    var handle = GCHandle.Alloc(frame.RawPixels, GCHandleType.Pinned);
            //    Utilities.CopyMemory(img.pixelBuffers[i].DataPointer, handle.AddrOfPinnedObject(), frame.RawPixels.Length);
            //    handle.Free();
            //}
            {
                var frame = gif.Frames[40];
                var handle = GCHandle.Alloc(frame.RawPixels, GCHandleType.Pinned);
                Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), frame.RawPixels.Length);
                handle.Free();
            }
            return img;
        }

        private void GetImageFromIndexStream(GifFrame frame, int frameIndex, GifImage gifImage)
        {
            var width = frame.Descriptor.Width;
            var height = frame.Descriptor.Height;
            var colorTable = frame.ColorTable;

            byte[] pixels = null;
            int offset = 0;

            if (frameIndex == 1)
            {

            }

            if (frameIndex == 0 || frame.GraphicControlExtension == null || (frame.GraphicControlExtension != null && frame.GraphicControlExtension.DisposalMethod == DisposalMethod.None))
            {
                pixels = new byte[width * height * 3];

                for (int i = 0; i < width * height; i++)
                {
                    var colors = colorTable[frame.IndexData[i]];
                    pixels[offset] = colors.R;
                    pixels[offset + 1] = colors.G;
                    pixels[offset + 2] = colors.B;
                    offset += 3;
                }
            }
            else if (frameIndex > 0 && frame.GraphicControlExtension != null && frame.GraphicControlExtension.DisposalMethod == DisposalMethod.DoNotDispose)
            {
                pixels = new byte[gifImage.Descriptor.Width * gifImage.Descriptor.Height * 3];
                var baseFrame = gifImage.Frames[frameIndex - 1];
                var originalIndexStream = baseFrame.IndexData;
                var currentIndexStream = new List<int>(originalIndexStream).ToArray();
                for (int i = 0; i < frame.Descriptor.Height; ++i)
                {
                    var dstIndex = ((frame.Descriptor.OffsetTop + i) * gifImage.Descriptor.Width) + frame.Descriptor.OffsetLeft;
                    var srcIndex = i * frame.Descriptor.Width;
                    Array.Copy(frame.IndexData, srcIndex, currentIndexStream, dstIndex, frame.Descriptor.Width);
                }

                var index = frame.IndexData[0];
                for (int i = 0; i < currentIndexStream.Length; i++)
                {
                    if (currentIndexStream[i] == frame.IndexData[0])
                    {
                        currentIndexStream[i] = originalIndexStream[i];
                    }

                    var colors = colorTable[currentIndexStream[i]];

                    pixels[offset] = colors.R;
                    pixels[offset + 1] = colors.G;
                    pixels[offset + 2] = colors.B;
                    offset += 3;
                }
                frame.IndexData = currentIndexStream;
                frame.RawPixels = pixels;
            }
            else
            {
                //throw new NotImplementedException($"Current disposal method: {frame.GraphicControlExtension.DisposalMethod} is not supported");
            }

            frame.RawPixels = pixels;
        }

        /// <summary>
        /// Read GIF frame
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="gifImage"></param>
        /// <param name="position"></param>
        private static unsafe void ProcessImageDescriptor(Stream stream, GifImage gifImage)
        {
            var descriptor = new GifImageDescriptor();
            descriptor.OffsetLeft = stream.ReadUInt16();
            descriptor.OffsetTop = stream.ReadUInt16();
            descriptor.Width = stream.ReadUInt16();
            descriptor.Height = stream.ReadUInt16();
            descriptor.Fields = (byte)stream.ReadByte();
            int interlace = descriptor.Fields & 0x40;
            GifFrame frame = new GifFrame();
            frame.Interlaced = Convert.ToBoolean(interlace);

            if ((descriptor.Fields & 0x80) != 0)
            {
                var size = 1 << ((descriptor.Fields & 0x07) + 1);
                //Read local color table
                var сolorTable = new byte[3 * size];
                stream.Read(сolorTable, 0, сolorTable.Length);
                int offset = 0;
                frame.ColorTable = new ColorRGB[size];
                for (int i = 0; i < size; i++)
                {
                    frame.ColorTable[i] = new ColorRGB(сolorTable[offset], сolorTable[offset + 1], сolorTable[offset + 2]);
                    offset += 3;
                }
            }
            else
            {
                frame.ColorTable = gifImage.GlobalColorTable.ToArray();
            }

            frame.Descriptor = descriptor;
            ReadImageData(stream, frame);
            gifImage.CurrentFrame = frame;
            gifImage.Frames.Add(frame);
        }

        /// Decompress image pixels.
        private static void ReadImageData(Stream stream, GifFrame frame)
        {
            frame.LzwMinimumCodeSize = (byte)stream.ReadByte();
            if (frame.LzwMinimumCodeSize > 8)
            {
                //throw new Exception($"LZW minimum code could not be more than 8. Current value is {frame.LzwMinimumCodeSize}");
            }
            byte blockSize;

            // Everything following are data sub-blocks, until a 0-sized block is
            // encountered.

            while (true)
            {
                var result = stream.ReadByte();
                if (result < 0)
                {
                    break;
                }

                blockSize = (byte)result;

                if (blockSize == 0)  // end of sub-blocks
                {
                    break;
                }

                var bytes = stream.ReadBytes(blockSize);
                frame.CompressedData.AddRange(bytes);
            }
        }

        private static unsafe void ProcessExtension(Stream stream, GifImage gifImage)
        {
            var extensionCode = (GifChunkCodes)stream.ReadByte();
            int blockSize = stream.ReadByte();
            var position = stream.Position;
            switch (extensionCode)
            {
                case GifChunkCodes.GraphicControl:
                    var graphicControlExtension = new GraphicControlExtension();
                    graphicControlExtension.Fields = (byte)stream.ReadByte();
                    graphicControlExtension.DelayTime = stream.ReadUInt16();
                    graphicControlExtension.TransparentColorIndex = (byte)stream.ReadByte();
                    graphicControlExtension.DisposalMethod = (DisposalMethod)(graphicControlExtension.Fields & 0x03);
                    if (gifImage.CurrentFrame != null)
                    {
                        gifImage.CurrentFrame.GraphicControlExtension = graphicControlExtension;
                    }
                    break;
                case GifChunkCodes.ApplicationExtension:
                    var applicationExtension = new ApplicationExtension();
                    applicationExtension.ApplicationId = Encoding.ASCII.GetString(stream.ReadBytes(8));
                    applicationExtension.Version = Encoding.ASCII.GetString(stream.ReadBytes(3));
                    gifImage.ApplicationExtension = applicationExtension;
                    if (applicationExtension.Version == "XMP")
                    {
                        var @byte = (byte)stream.ReadByte();
                        var bytes = new List<byte>() { @byte};
                        while(@byte != 0)
                        {
                            @byte = (byte)stream.ReadByte();
                            bytes.Add(@byte);
                            //var result = stream.ReadBytes(size);
                            //bytes.AddRange(result);
                            //size = stream.ReadByte();
                        }
                        var resultArray = bytes.ToArray()[..^257];
                        var str = Encoding.UTF8.GetString(resultArray);
                        blockSize += bytes.Count;
                    }

                    break;
                case GifChunkCodes.CommentExtension:
                    // comment extension; do nothing - all the data is in the
                    // sub-blocks that follow.
                    break;
                case GifChunkCodes.PlainTextExtension:
                    var plainTextExtension = new PlainTextExtension();
                    plainTextExtension.Left = stream.ReadUInt16();
                    plainTextExtension.Top = stream.ReadUInt16();
                    plainTextExtension.Width = stream.ReadUInt16();
                    plainTextExtension.Height = stream.ReadUInt16();
                    plainTextExtension.CellWidth = (byte)stream.ReadByte();
                    plainTextExtension.CellHeight = (byte)stream.ReadByte();
                    plainTextExtension.ForegroundColor = (byte)stream.ReadByte();
                    plainTextExtension.BckgroundColor = (byte)stream.ReadByte();
                    break;
                default:
                    break;
            }
            stream.Position = position;
            stream.Position += blockSize;
        }

        private static bool ReadGIFHeader(Stream stream)
        {
            var bytes = stream.ReadBytes(6);
            var headerString = Encoding.ASCII.GetString(bytes);
            if (headerString != GIFHeader)
            {
                return false;
            }

            return true;
        }
    }
}
