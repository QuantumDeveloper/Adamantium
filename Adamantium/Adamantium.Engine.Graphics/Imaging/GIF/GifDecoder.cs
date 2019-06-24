using Adamantium.Core;
using Adamantium.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            //var bytes = stream.ReadBytes(2);
            //var result = bytes[0] + (bytes[1] << 8);
            //stream.Seek(0, SeekOrigin.Begin);
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
                for (int i = 0; i < сolorTable.Length; i += 3)
                {
                    gifImage.GlobalColorTable.Add(new ColorRGB(сolorTable[i], сolorTable[i + 1], сolorTable[i + 2]));
                }
            }

            GifChunkCodes blockType = GifChunkCodes.None;

            while (blockType != GifChunkCodes.Trailer)
            {
                blockType = (GifChunkCodes)stream.ReadByte();

                switch (blockType)
                {
                    case GifChunkCodes.ImageDescriptor:
                        ProcessImageDescriptor(stream, gifImage, stream.PositionPointer);
                        break;
                    case GifChunkCodes.ExtensionIntroducer:
                        ProcessExtension(stream);
                        break;
                    case GifChunkCodes.Trailer:
                        break;
                }
            }

            DecodeInternal(gifImage);
            return null;
        }

        struct DictionaryEntry
        {
            public byte @byte;
            public int prev;
            public int len;
        }

        private void DecodeInternal(GifImage gif)
        {
            var frame = gif.Frames[0];

            uint mask = 0x01;
            int i;
            int index;
            int code, prev = -1;
            int codeLength;
            int resetCodeLength; //This varies depending on codeLength
            int clearCode;
            int stopCode;
            int matchLen;


            codeLength = frame.ImageData.LzwMinimumCodeSize;
            clearCode = 1 << codeLength;
            stopCode = clearCode + 1;
            resetCodeLength = codeLength;

            var inputData = frame.ImageData.blockBytes;
            var codeTable = new Dictionary<int, List<int>>();
            var dictionary = new DictionaryEntry[1 << (codeLength + 1)];

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

            var indexStream = new List<int>();
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
                    codeTable.Clear();
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

                    //Special handling for KwKwK
                    if (code == index)
                    {
                        int ptr = prev;
                        while (dictionary[ptr].prev != -1)
                        {
                            ptr = dictionary[ptr].prev;
                        }
                        dictionary[index].@byte = dictionary[ptr].@byte;
                    }
                    else
                    {
                        int ptr = code;
                        while (dictionary[ptr].prev != -1)
                        {
                            ptr = dictionary[ptr].prev;
                        }
                        dictionary[index].@byte = dictionary[ptr].@byte;
                    }

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
                matchLen = dictionary[code].len;
                while (code != -1)
                {
                    indexStream.Add(dictionary[code].@byte);
                    if (dictionary[code].prev == code)
                    {
                        throw new Exception("INternal error; self-reference");
                    }
                    code = dictionary[code].prev;
                }
            }
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
            if ((gifImageDescriptor.fields & 0x80) != 0)
            {
                //Read local color table
                var size = 1 << ((gifImageDescriptor.fields & 0x07) + 1);
                var сolorTable = new byte[3 * size];
                stream.Read(сolorTable, 0, сolorTable.Length);
                int cnt = 0;
                frame.ColorTable = new ColorRGB[size];
                for (int i = 0; i < size; i += 3)
                {
                    frame.ColorTable[i] = new ColorRGB(сolorTable[i], сolorTable[i + 1], сolorTable[i + 2]);
                    cnt++;
                }
            }
            else
            {
                frame.ColorTable = gifImage.GlobalColorTable.ToArray();
            }

            var imageData = ReadImageData(stream);
            frame.ImageData = imageData;
            imageData.colorTable = frame.ColorTable;
            gifImage.Frames.Add(frame);
        }

        /// Decompress image pixels.
        private static GifImageData ReadImageData(Stream stream)
        {
            GifImageData imageData = new GifImageData();
            byte lzwCodeSize = (byte)stream.ReadByte();
            imageData.LzwMinimumCodeSize = lzwCodeSize;

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
                imageData.blockBytes.AddRange(bytes);
            }

            return imageData;
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
