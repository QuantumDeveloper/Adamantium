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
    internal class GifImageData
    {
        private BitArray _blockBits;
        private int _currentCodeSize;
        private Dictionary<int, ColorRGB> _colors;

        public int lzwMinimumCodeSize;
        public int endingOffset;
        public List<byte> blockBytes;
        public Dictionary<int, int[]> codeTable;
        public List<int> colorIndices;
        //public GifGraphicsControlExtension graphicsControlExt;
        public GifImageDescriptor imageDescriptor;
        //public GifFrame _frame;
        public ColorRGB[] colorTable;

        public GifImageData()
        {
            _colors = new Dictionary<int, ColorRGB>(256);

            codeTable = new Dictionary<int, int[]>(4096);
            colorIndices = new List<int>(256);
            blockBytes = new List<byte>(255);
        }

        public void decode()
        {
            // Convert bytes to bits
            _blockBits = new BitArray(blockBytes.ToArray());
            //Debug.Log("Converted " + blockBytes.Count + " bytes into " + _blockBits.Length + " bits.");

            // Translate block
            translateBlock();

            // Prepare colors
            prepareColors();
        }

        private void translateBlock()
        {
            _currentCodeSize = lzwMinimumCodeSize + 1;
            int currentCode;
            int previousCode;
            int bitOffset = _currentCodeSize;
            int iteration = 0;
            int cc = 1 << lzwMinimumCodeSize;
            int eoi = cc + 1;

            //Debug.Log("Starting to translate block. Current code size: " + _currentCodeSize +", CC: " + cc + ", EOI: " + eoi);

            initializeCodeTable();
            currentCode = GetCode(_blockBits, bitOffset, _currentCodeSize);
            addToColorIndices(getCodeValues(currentCode));
            previousCode = currentCode;
            bitOffset += _currentCodeSize;

            while (true)
            {
                currentCode = GetCode(_blockBits, bitOffset, _currentCodeSize);
                bitOffset += _currentCodeSize;

                // Handle special codes
                if (currentCode == cc)
                {
                    //Debug.Log("Encountered CC. Reinitializing code table...");
                    _currentCodeSize = lzwMinimumCodeSize + 1;
                    initializeCodeTable();
                    currentCode = GetCode(_blockBits, bitOffset, _currentCodeSize);
                    addToColorIndices(getCodeValues(currentCode));
                    previousCode = currentCode;
                    bitOffset += _currentCodeSize;
                    continue;
                }
                else if (currentCode == eoi)
                {
                    break;
                }

                // Does code table contain the current code
                if (codeTable.ContainsKey(currentCode))
                {
                    int[] newEntry;
                    int[] previousValues;
                    int[] currentValues;

                    addToColorIndices(getCodeValues(currentCode));
                    previousValues = getCodeValues(previousCode);
                    currentValues = getCodeValues(currentCode);
                    newEntry = new int[previousValues.Length + 1];

                    for (int i = 0; i < previousValues.Length; i++)
                    {
                        newEntry[i] = previousValues[i];
                    }
                    newEntry[newEntry.Length - 1] = currentValues[0];

                    addToCodeTable(newEntry);
                    previousCode = currentCode;
                }
                else
                {
                    int[] previousValues = getCodeValues(previousCode);
                    int[] indices = new int[previousValues.Length + 1];

                    for (int i = 0; i < previousValues.Length; i++)
                    {
                        indices[i] = previousValues[i];
                    }
                    indices[indices.Length - 1] = previousValues[0];

                    addToCodeTable(indices);
                    addToColorIndices(indices);
                    previousCode = currentCode;
                }

                iteration++;

                // Infinite loop exit check
                if (iteration > 999999)
                {
                    throw new Exception("Too many iterations. Infinite loop.");
                }
            }
        }

        private void addToCodeTable(int[] entry)
        {
            string indices = "";

            for (int i = 0; i < entry.Length; i++)
            {
                indices += entry[i];
                indices += (i < entry.Length - 1) ? ", " : "";
            }

            //Debug.Log("Adding code " + codeTable.Count + " to code table with values: " + indices);

            if (codeTable.Count == (1 << _currentCodeSize) - 1)
            {
                _currentCodeSize++;
                //Debug.Log("Increasing current code size to: " + _currentCodeSize);

                if (_currentCodeSize > 12)
                {
                    throw new NotImplementedException("Code size larger than max (12). Figure out how to handle this.");
                }
            }

            if (codeTable.Count >= 4096)
            {
                //throw new Exception("Exceeded max number of entries in code table.");
            }

            codeTable.Add(codeTable.Count, entry);
        }

        private void addToColorIndices(int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                colorIndices.Add(indices[i]);
            }
        }

        private bool isMaxCodeValue(int currentCode, int currentCodeSize)
        {
            return currentCode == (1 << currentCodeSize) - 1;
        }

        private void initializeCodeTable()
        {
            int initialCodeTableSize = (1 << lzwMinimumCodeSize) + 1;

            codeTable.Clear();
            for (int i = 0; i <= initialCodeTableSize; i++)
            {
                codeTable.Add(i, new int[] { i });
            }

            //Debug.Log("Initialized code table. Highest index: " + (codeTable.Count - 1));
        }

        private int GetCode(BitArray bits, int bitOffset, int currentCodeSize)
        {
            int value = 0;
            string debugValue = "";

            // Max code size check
            if (currentCodeSize > 12)
            {
                throw new ArgumentOutOfRangeException("currentCodeSize", "Max code size is 12");
            }

            // Calculate value
            for (int i = 0; i < currentCodeSize; i++)
            {
                int index = bitOffset + i;

                if (bits[index])
                {
                    value += (1 << i);
                    debugValue += "1";
                }
                else
                {
                    debugValue += "0";
                }
            }

            //Debug.Log("Read code [" + value + "(" + debugValue + ")] at bit offset [" + bitOffset + "] using code size [" + currentCodeSize + "]");

            return value;
        }

        private int[] getCodeValues(int code)
        {
            if (codeTable.ContainsKey(code))
            {
                return codeTable[code];
            }
            else
            {
                throw new Exception("Code " + code + " does not exist. Code table size: " + codeTable.Count + ". Aborting...");
            }
        }

        private void prepareColors()
        {
            foreach (int index in colorIndices)
            {
                if (!_colors.ContainsKey(index))
                {
                    _colors.Add(index, colorTable[index]);
                }
            }
        }

        public ColorRGB getColor(int index)
        {
            return _colors[index];
        }
    }

    internal struct DictionaryEntry
    {
        public byte @byte;
        public int prev;
        public int len;
    }

    public static class GIFHelper
    {
        private const string GIFHeader = "GIF89a";

        public static unsafe Image LoadFromGifMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            var stream = new UnmanagedMemoryStream((byte*)pSource, (long)size);
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
                for (int i = 0; i<сolorTable.Length; i+=3)
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

            return null;
        }

        private static unsafe void ProcessExtension(Stream stream)
        {
            var ptr = ((UnmanagedMemoryStream)stream).PositionPointer;
            var ext = *(GifExtension*)ptr;
            stream.Position += Marshal.SizeOf<GifExtension>();
            GraphicControlExtension graphicControlExtension;
            ApplicationExtension applicationExtension;
            PlainTextExtension plainTextExtension;
            var position = stream.Position;
            switch (ext.extensionCode)
            {
                case GifChunkCodes.GraphicControl:
                    ptr = ((UnmanagedMemoryStream)stream).PositionPointer;
                    graphicControlExtension = *(GraphicControlExtension*)ptr;
                    
                    break;
                case GifChunkCodes.ApplicationExtension:
                    applicationExtension = new ApplicationExtension();
                    applicationExtension.applicationId = stream.ReadBytes(8);
                    applicationExtension.version = stream.ReadBytes(3);
                    break;
                case GifChunkCodes.CommentExtension:
                    // comment extension; do nothing - all the data is in the
                    // sub-blocks that follow.
                    break;
                case GifChunkCodes.PlainTextExtension:
                    ptr = ((UnmanagedMemoryStream)stream).PositionPointer;
                    plainTextExtension = *(PlainTextExtension*)ptr;
                    break;
                default:
                    break;
            }
            stream.Position = position;
            stream.Position += ext.blockSize;
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
                var size = 1 <<((gifImageDescriptor.fields & 0x07) + 1);
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

            gifImage.Frames.Add(frame);
            var imageData = ReadImageData(stream);
            imageData.colorTable = frame.ColorTable;
            //imageData.decode();
        }

        /// Decompress image pixels.
        private static GifImageData ReadImageData(Stream stream)
        {
            GifImageData imageData = new GifImageData();
            byte lzwCodeSize = (byte)stream.ReadByte();
            imageData.lzwMinimumCodeSize = lzwCodeSize;

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

        public static unsafe void SaveToGIFStream(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {

        }
    }
}
