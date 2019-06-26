using Adamantium.Core;
using Adamantium.Mathematics;
using System;
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

        public unsafe Image Decode(UnmanagedMemoryStream stream)
        {
            if (!ReadGIFHeader(stream))
            {
                throw new ArgumentException("Given file is not a GIF file");
            }

            var gifImage = new GifImage();
            ScreenDescriptor screenDescriptor = *(ScreenDescriptor*)stream.PositionPointer;
            stream.Position += Marshal.SizeOf<ScreenDescriptor>();

            /* Color Space's Depth */
            gifImage.ColorDepth = (byte)(((screenDescriptor.fields >> 4) & 7) + 1);
            /* Ignore Sort Flag. */
            /* GCT Size */
            var gctSize = 1 << ((screenDescriptor.fields & 0x07) + 1);

            /* Presence of GCT (global color table) */
            if ((screenDescriptor.fields & 0x80) != 0)
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
            int frameIndex = 0;
            while (blockType != GifChunkCodes.Trailer)
            {
                blockType = (GifChunkCodes)stream.ReadByte();

                switch (blockType)
                {
                    case GifChunkCodes.ImageDescriptor:
                        if (frameIndex == 54)
                        {

                        }
                        ProcessImageDescriptor(stream, gifImage, stream.PositionPointer);
                        frameIndex++;
                        frameIndex++;
                        break;
                    case GifChunkCodes.ExtensionIntroducer:
                        ProcessExtension(stream);
                        break;
                    case GifChunkCodes.Trailer:
                        break;
                }
            }

            //return DecodeInternal(gifImage);
            return DecodeInternalAlternative(gifImage);
        }

        struct DictionaryEntry
        {
            public byte @byte;
            public int prev;
            public int len;
        }

        private unsafe Image DecodeInternal(GifImage gif)
        {
            var frame = gif.Frames[20];

            uint mask = 0x01;
            int i;
            int index;
            int code, prev = -1;
            int codeLength;
            int resetCodeLength; //This varies depending on codeLength
            int clearCode;
            int stopCode;
            int streamOffset = 0;

            codeLength = frame.LzwMinimumCodeSize;
            clearCode = 1 << codeLength;
            stopCode = clearCode + 1;
            resetCodeLength = codeLength;

            var inputData = frame.CompressedData;
            var dictionary = new DictionaryEntry[1 << (codeLength + 1)];

            var width = gif.Descriptor.width;
            var height = gif.Descriptor.height;
            var colorTable = frame.ColorTable;

            //init dictionary
            for (index = 0; index < (1 << codeLength); index++)
            {
                //codeTable.Add(index, new List<int>() { index });
                var entry = new DictionaryEntry();

                entry.@byte = (byte)index;
                entry.prev = -1;
                entry.len = 1;

                dictionary[index] = entry;
            }

            // 2^code_len + 1 is the special "end" code; don't give it an entry here
            index++;
            index++;

            var indexStream = new int[width * height];
            int inputOffset = 0;
            int inputLength = inputData.Count;

            while (inputLength > 0)
            {
                code = 0x0;
                for (i = 0; i < (codeLength + 1); i++)
                {
                    var val = inputData[inputOffset];
                    int bit = (val & mask) != 0 ? 1 : 0;
                    mask <<= 1;

                    if (mask == 0x100)
                    {
                        mask = 0x01;
                        inputOffset++;
                        inputLength--;
                    }

                    code |= (bit << i);
                }

                if (code == clearCode)
                {
                    codeLength = resetCodeLength;
                    //init dictionary
                    dictionary = new DictionaryEntry[1 << (codeLength + 1)];
                    for (index = 0; index < (1 << codeLength); index++)
                    {
                        //codeTable.Add(index, new List<int>() { index });
                        var entry = new DictionaryEntry();

                        entry.@byte = (byte)index;
                        entry.prev = -1;
                        entry.len = 1;

                        dictionary[index] = entry;
                    }
                    index++;
                    index++;
                    prev = -1;
                    continue;
                }
                else if (code == stopCode)
                {
                    if (inputLength > 1)
                    {
                        throw new Exception("Malformed GIF (early stop code)");
                    }
                    break;
                }

                // Update the dictionary with this character plus the _entry_
                // (character or string) that came before it
                if ((prev > -1) && (codeLength < 12))
                {
                    if (code > index)
                    {
                        throw new Exception($"code = {code}, but index = {index}");
                    }

                    int ptr;
                    //Special handling for KwKwK
                    if (code == index)
                    {
                        ptr = prev;
                    }
                    else
                    {
                        ptr = code;
                    }

                    while (dictionary[ptr].prev != -1)
                    {
                        ptr = dictionary[ptr].prev;
                    }

                    dictionary[index].@byte = dictionary[ptr].@byte;

                    dictionary[index].prev = prev;
                    dictionary[index].len = dictionary[prev].len + 1;
                    index++;

                    // GIF89a mandates that this stops at 12 bits
                    if ((index == (1 << (codeLength + 1))) &&
                        codeLength < 11)
                    {
                        codeLength++;
                        Array.Resize(ref dictionary, (1 << (codeLength + 1)));
                    }
                }

                prev = code;
                // Now copy the dictionary entry backwards into "indexStream"
                var matchLen = dictionary[code].len;
                fixed (int* indexPtr = &indexStream[streamOffset])
                {
                    //int* indexCopy = &indexPtr[streamOffset];
                    while (code != -1)
                    {
                        indexPtr[dictionary[code].len - 1] = dictionary[code].@byte;
                        if (dictionary[code].prev == code)
                        {
                            throw new Exception("Internal error; self-reference");
                        }
                        code = dictionary[code].prev;
                    }
                }
                streamOffset += matchLen;
            }

            var pixels = new byte[width * height * 3];
            int offset = 0;
            for (i = 0; i < width * height; i++)
            {
                var colors = colorTable[indexStream[i]];
                pixels[offset] = colors.R;
                pixels[offset + 1] = colors.G;
                pixels[offset + 2] = colors.B;
                offset += 3;
            }

            ImageDescription description = new ImageDescription();
            description.Width = width;
            description.Height = height;
            description.MipLevels = 1;
            description.Dimension = TextureDimension.Texture2D;
            description.Format = AdamantiumVulkan.Core.Format.R8G8B8_UNORM;
            description.ArraySize = 1;
            description.Depth = 1;

            var img = Image.New(description);
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), pixels.Length);
            handle.Free();

            return img;
        }

        private Image DecodeInternalAlternative(GifImage gif)
        {
            var frame = gif.Frames[70];
            var data = frame.CompressedData;
            var minCodeSize = frame.LzwMinimumCodeSize;
            uint mask = 0x01;
            int inputLength = data.Count;

            var pos = 0;
            int readCode(int size)
            {
                int code = 0x0;
                for (var i = 0; i < size; i++)
                {
                    var val = data[pos];
                    int bit = (val & mask) != 0 ? 1 : 0;
                    mask <<= 1;

                    if (mask == 0x100)
                    {
                        mask = 0x01;
                        pos++;
                        inputLength--;
                    }

                    code |= (bit << i);
                }
                return code;
            };

            var indexStream = new List<int>();

            var clearCode = 1 << minCodeSize;
            var eoiCode = clearCode + 1;

            var codeSize = minCodeSize + 1;

            var dict = new List<List<int>>();

            void Clear()
            {
                dict.Clear();
                codeSize = frame.LzwMinimumCodeSize + 1;
                for (int i = 0; i < clearCode; i++)
                {
                    dict.Add(new List<int>() { i });
                }
                dict.Add(new List<int>());
                dict.Add(null);
            }

            int code = 0x0;
            int last = 0;

            while (inputLength > 0)
            {
                last = code;
                code = readCode(codeSize);

                if (code == clearCode)
                {
                    Clear();
                    continue;
                }
                if (code == eoiCode)
                {
                    break;
                }

                if (code < dict.Count)
                {
                    if (last != clearCode)
                    {
                        var lst = new List<int>(dict[last]);
                        lst.Add(dict[code][0]);
                        dict.Add(lst);
                    }
                }
                else
                {
                    if (last != clearCode)
                    {
                        var lst = new List<int>(dict[last]);
                        lst.Add(dict[last][0]);
                        dict.Add(lst);
                    }
                }
                
                indexStream.AddRange(dict[code]);

                if (dict.Count == (1 << codeSize) && codeSize < 12)
                {
                    // If we're at the last code and codeSize is 12, the next code will be a clearCode, and it'll be 12 bits long.
                    codeSize++;
                }
            }

            var width = frame.Descriptor.width;
            var height = frame.Descriptor.height;
            var colorTable = frame.ColorTable;

            var pixels = new byte[width * height * 3];
            int offset = 0;
            for (int i = 0; i < width * height; i++)
            {
                var colors = colorTable[indexStream[i]];
                pixels[offset] = colors.R;
                pixels[offset + 1] = colors.G;
                pixels[offset + 2] = colors.B;
                offset += 3;
            }

            ImageDescription description = new ImageDescription();
            description.Width = width;
            description.Height = height;
            description.MipLevels = 1;
            description.Dimension = TextureDimension.Texture2D;
            description.Format = AdamantiumVulkan.Core.Format.R8G8B8_UNORM;
            description.ArraySize = 1;
            description.Depth = 1;

            var img = Image.New(description);
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), pixels.Length);
            handle.Free();

            return img;
        }

        /// <summary>
        /// Read GIF frame
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="gifImage"></param>
        /// <param name="position"></param>
        private static unsafe void ProcessImageDescriptor(Stream stream, GifImage gifImage, byte* position)
        {
            GifImageDescriptor gifImageDescriptor = *(GifImageDescriptor*)position;
            stream.Position += Marshal.SizeOf<GifImageDescriptor>();
            int interlace = gifImageDescriptor.fields & 0x40;
            GifFrame frame = new GifFrame();
            frame.Interlaced = Convert.ToBoolean(interlace);
            if ((gifImageDescriptor.fields & 0x80) != 0)
            {
                //Read local color table
                var size = 1 << ((gifImageDescriptor.fields & 0x07) + 1);
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

            frame.Descriptor = gifImageDescriptor;
            ReadImageData(stream, frame);
            gifImage.Frames.Add(frame);
        }

        /// Decompress image pixels.
        private static void ReadImageData(Stream stream, GifFrame frame)
        {
            frame.LzwMinimumCodeSize = (byte)stream.ReadByte();

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

        private static unsafe void ProcessExtension(Stream stream)
        {
            var extensionCode = (GifChunkCodes)stream.ReadByte();
            var blockSize = (byte)stream.ReadByte();
            var position = stream.Position;
            switch (extensionCode)
            {
                case GifChunkCodes.GraphicControl:
                    var graphicControlExtension = new GraphicControlExtension();
                    graphicControlExtension.fields = (byte)stream.ReadByte();
                    graphicControlExtension.delayTime = stream.ReadUInt16();
                    graphicControlExtension.transparentColorIndex = (byte)stream.ReadByte();
                    break;
                case GifChunkCodes.ApplicationExtension:
                    var applicationExtension = new ApplicationExtension();
                    applicationExtension.applicationId = Encoding.ASCII.GetString(stream.ReadBytes(8));
                    applicationExtension.version = Encoding.ASCII.GetString(stream.ReadBytes(3));
                    break;
                case GifChunkCodes.CommentExtension:
                    // comment extension; do nothing - all the data is in the
                    // sub-blocks that follow.
                    break;
                case GifChunkCodes.PlainTextExtension:
                    var plainTextExtension = new PlainTextExtension();
                    plainTextExtension.left = stream.ReadUInt16();
                    plainTextExtension.top = stream.ReadUInt16();
                    plainTextExtension.width = stream.ReadUInt16();
                    plainTextExtension.height = stream.ReadUInt16();
                    plainTextExtension.cellWidth = (byte)stream.ReadByte();
                    plainTextExtension.cellHeight = (byte)stream.ReadByte();
                    plainTextExtension.foregroundColor = (byte)stream.ReadByte();
                    plainTextExtension.bckgroundColor = (byte)stream.ReadByte();
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
