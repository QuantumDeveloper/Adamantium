// Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.
// Under the MIT License, details: License.txt.

using System;
using System.Collections.Generic;
using Adamantium.Imaging.Jpeg.IO;

#if DYNAMIC_IDCT
using System.Reflection.Emit;
#endif

namespace Adamantium.Imaging.Jpeg.Decoder
{
    internal class JpegComponent
    {
        public byte factorH, factorV, component_id, quant_id;
        public int width = 0, height = 0;
        public HuffmanTable ACTable;
        public HuffmanTable DCTable;

        public int[] QuantizationTable
        {
            set
            {
                quantizationTable = value;
#if DYNAMIC_IDCT
                _quant = EmitQuantize();
#endif
            }
        }
        private int[] quantizationTable;

        public float previousDC = 0;
        private JpegScan parent;

        // Current MCU block
        float[,][] scanMCUs = null;

        private List<float[,][]> scanData = new List<float[,][]>();

        public int BlockCount { get { return scanData.Count; } }

        private List<byte[,]> scanDecoded = new List<byte[,]>();

        // THE DECODED BLOCKS, all of them, in one array of 64 bytes each.
        //
        // They used to be a List of byte[8,8], one object per block - about 400 000 of them per component in a 4K
        // photograph, every one a separate allocation with its own header, and every read of a pixel a multidimensional
        // index. One buffer costs a single allocation, indexes by arithmetic the JIT can hoist, and lets the whole set
        // be walked in memory order.
        private byte[] decoded;
        private int decodedCount;

        private const int BlockSide = 8;
        private const int BlockPixels = BlockSide * BlockSide;

        public int spectralStart, spectralEnd;
        public int successiveLow;

        public JpegComponent(JpegScan parentScan, byte id, byte factorHorizontal, byte factorVertical,
                             byte quantizationID, byte colorMode)
        {
            parent = parentScan;

            /* Set default tables in case they're not provided.  J. Powers */
            // TODO: only gen if needed

            if (colorMode == JpegFrame.JPEG_COLOR_YCbCr)
            {
                if (id == 1) // Luminance
                {
                    ACTable = new HuffmanTable(JpegHuffmanTable.StdACLuminance);
                    DCTable = new HuffmanTable(JpegHuffmanTable.StdDCLuminance);
                }
                else
                {
                    ACTable = new HuffmanTable(JpegHuffmanTable.StdACChrominance);
                    DCTable = new HuffmanTable(JpegHuffmanTable.StdACLuminance);
                }
            }

            component_id = id;

            factorH = factorHorizontal;
            factorV = factorVertical;

            quant_id = quantizationID;
        }

        /// <summary>
        /// If a restart marker is found with too little of an MCU count (i.e. our
        /// Restart Interval is 63 and we have 61 we copy the last MCU until it's full)
        /// </summary>
        public void padMCU(int index, int length)
        {
            scanMCUs = new float[factorH, factorV][];

            for (int n = 0; n < length; n++)
            {
                if (scanData.Count >= index + length) continue;

                for (int i = 0; i < factorH; i++)
                    for (int j = 0; j < factorV; j++)
                        scanMCUs[i, j] = (float[])scanData[index - 1][i, j].Clone();

                scanData.Add(scanMCUs);
            }

        }

        /// <summary>
        /// Reset the interval by setting the previous DC value
        /// </summary>
        public void resetInterval()
        {
            previousDC = 0;
        }

#if DYNAMIC_IDCT
        private delegate void QuantizeDel(float[] arr);
        private QuantizeDel _quant = null;

        private QuantizeDel EmitQuantize()
        {
            Type[] args = { typeof(float[]) };

            DynamicMethod quantizeMethod = new DynamicMethod("Quantize",
                null, // no return type
                args); // input array

            ILGenerator il = quantizeMethod.GetILGenerator();

            for (int i = 0; i < quantizationTable.Length; i++)
            {
                float mult = (float)quantizationTable[i];

                                                       // Sz Stack:
                il.Emit(OpCodes.Ldarg_0);              // 1  {arr} 
                il.Emit(OpCodes.Ldc_I4_S, (short)i);   // 3  {arr,i}
                il.Emit(OpCodes.Ldarg_0);              // 1  {arr,i,arr}
                il.Emit(OpCodes.Ldc_I4_S, (short)i);   // 3  {arr,i,arr,i}
                il.Emit(OpCodes.Ldelem_R4);            // 1  {arr,i,arr[i]}
                il.Emit(OpCodes.Ldc_R4, mult);         // 5  {arr,i,arr[i],mult}
                il.Emit(OpCodes.Mul);                  // 1  {arr,i,arr[i]*mult}
                il.Emit(OpCodes.Stelem_R4);            // 1  {}

            }

            il.Emit(OpCodes.Ret);

            return (QuantizeDel)quantizeMethod.CreateDelegate(typeof(QuantizeDel));
        }
#endif

        /// <summary>
        /// Run the Quantization backward method on all of the block data.
        /// </summary>
        public void quantizeData()
        {
            for (int i = 0; i < scanData.Count; i++)
            {
                for (int v = 0; v < factorV; v++)
                    for (int h = 0; h < factorH; h++)
                    {
#if DYNAMIC_IDCT
                        // Dynamic IL method
                        _quant(scanData[i][h, v]);
#else
                        // Old technique
                        float[] toQuantize = scanData[i][h, v];
                        for (int j = 0; j < quantizationTable.Length; j++) toQuantize[j] *= quantizationTable[j];
#endif
                    }
            }

        }

        public void setDCTable(JpegHuffmanTable table)
        {
            DCTable = new HuffmanTable(table);
        }

        public void setACTable(JpegHuffmanTable table)
        {
            ACTable = new HuffmanTable(table);
        }

        DCT _dct = new DCT();

        /// <summary>
        /// Run the Inverse DCT method on all of the block data
        /// </summary>
        public void idctData()
        {
            float[] unZZ = new float[64];

            // Sized up front, from what the scan actually holds - the count is known, so the buffer is allocated once
            // instead of growing a list a block at a time.
            decodedCount = scanData.Count * factorV * factorH;
            decoded = new byte[decodedCount * BlockPixels];

            var offset = 0;
            for (int i = 0; i < scanData.Count; i++)
            {
                for (int v = 0; v < factorV; v++)
                    for (int h = 0; h < factorH; h++)
                    {
                        ZigZag.UnZigZag(scanData[i][h, v], unZZ);
                        _dct.FastIDCT(unZZ, decoded, offset);
                        offset += BlockPixels;
                    }
            }
        }

        private int factorUpV { get { return parent.MaxV / factorV; } }
        private int factorUpH { get { return parent.MaxH / factorH; } }


        /// <summary>
        /// Stretches components as needed to normalize the size of all components.
        /// For example, in a 2x1 (4:2:2) sequence, the Cr and Cb channels will be 
        /// scaled vertically by a factor of 2.
        /// </summary>
        public void scaleByFactors(BlockUpsamplingMode mode)
        {
            int factorUpVertical = factorUpV,
                factorUpHorizontal = factorUpH;

            if (factorUpVertical == 1 && factorUpHorizontal == 1) return;

            for (int i = 0; i < scanDecoded.Count; i++)
            {
                byte[,] src = scanDecoded[i];

                int oldV = src.GetLength(0),
                    oldH = src.GetLength(1),
                    newV = oldV * factorUpVertical,
                    newH = oldH * factorUpHorizontal;

                byte[,] dest = new byte[newV, newH];

                switch (mode)
                {
                    case BlockUpsamplingMode.BoxFilter:
                        #region Upsampling by repeating values
                        /* Perform scaling (Box filter) */
                        for (int u = 0; u < newH; u++)
                        {
                            int src_u = u / factorUpHorizontal;
                            for (int v = 0; v < newV; v++)
                            {
                                int src_v = v / factorUpVertical;
                                dest[v, u] = src[src_v, src_u];
                            }
                        }
                        #endregion
                        break;

                    case BlockUpsamplingMode.Interpolate:
                        #region Upsampling by interpolation

                        for (int u = 0; u < newH; u++)
                        {
                            for (int v = 0; v < newV; v++)
                            {
                                int val = 0;

                                for (int x = 0; x < factorUpHorizontal; x++)
                                {
                                    int src_u = (u + x) / factorUpHorizontal;
                                    if (src_u >= oldH) src_u = oldH - 1;

                                    for (int y = 0; y < factorUpVertical; y++)
                                    {
                                        int src_v = (v + y) / factorUpVertical;

                                        if (src_v >= oldV) src_v = oldV - 1;

                                        val += src[src_v, src_u];
                                    }
                                }

                                dest[v, u] = (byte)(val / (factorUpHorizontal * factorUpVertical));
                            }
                        }

                        #endregion
                        break;

                    default:
                        throw new ArgumentException("Upsampling mode not supported.");
                }

                scanDecoded[i] = dest;
            }

        }


        public void writeBlock(byte[][,] raster, byte[,] data,
                               int compIndex, int x, int y)
        {
            int w = raster[0].GetLength(0),
                h = raster[0].GetLength(1);

            byte[,] comp = raster[compIndex];

            // Blocks may spill over the frame so we bound by the frame size
            int yMax = data.GetLength(0); if (y + yMax > h) yMax = h - y;
            int xMax = data.GetLength(1); if (x + xMax > w) xMax = w - x;

            for (int yIndex = 0; yIndex < yMax; yIndex++)
            {
                for (int xIndex = 0; xIndex < xMax; xIndex++)
                {
                    comp[x + xIndex, y + yIndex] = data[yIndex, xIndex];
                }
            }
        }

        /// <summary>
        /// The preview path: every block becomes ONE pixel, its DC coefficient.
        ///
        /// <para>A block's DC term is the average of its 64 samples (scaled by 8, which is why it is divided by that
        /// after dequantizing). So reading it alone gives the picture at an eighth of its size directly, with no
        /// inverse transform to run - and the whole point of this method is what it does NOT do.</para>
        ///
        /// <para>The traversal mirrors <see cref="writeDataScaled"/> exactly, since the layout of blocks into MCUs is
        /// the same; only the tile each block covers shrinks from 8x8 to 1x1, subsampling included - a chroma plane at
        /// 4:2:0 still stretches its pixel over the two-by-two the luma covers.</para>
        /// </summary>
        public void writeDcScaled(byte[][,] raster, int componentIndex)
        {
            var plane = raster[componentIndex];
            int w = plane.GetLength(0),
                h = plane.GetLength(1);

            var upH = factorUpH;
            var upV = factorUpV;
            var dcQuant = quantizationTable[0];

            // The SAME walk as writeDataScaled - carried along block by block rather than computed from an index,
            // because that is what gets the partial blocks at the right and bottom edges right. Only the tile size
            // differs: one pixel per block instead of eight.
            int x = 0, y = 0, lastblockheight = 0, incrementblock = 0;
            int blockIdx = 0;
            var total = scanData.Count * factorV * factorH;

            while (blockIdx < total)
            {
                int blockwidth = 0;
                int blockheight = 0;

                if (x >= w) { x = 0; y += incrementblock; }

                for (int factorVIndex = 0; factorVIndex < factorV; factorVIndex++)
                {
                    blockwidth = 0;

                    for (int factorHIndex = 0; factorHIndex < factorH; factorHIndex++)
                    {
                        var mcu = blockIdx / (factorV * factorH);
                        var within = blockIdx % (factorV * factorH);
                        var dc = scanData[mcu][within % factorH, within / factorH][0] * dcQuant / 8f + 128f;
                        var value = dc < 0 ? (byte)0 : dc > 255 ? (byte)255 : (byte)(dc + 0.5f);

                        for (int dx = 0; dx < upH; dx++)
                        {
                            var px = x + dx;
                            if (px >= w) break;

                            for (int dy = 0; dy < upV; dy++)
                            {
                                var py = y + dy;
                                if (py >= h) break;
                                plane[px, py] = value;
                            }
                        }

                        blockIdx++;
                        blockwidth += upH;
                        x += upH;
                        blockheight = upV;
                    }

                    y += blockheight;
                    x -= blockwidth;
                    lastblockheight += blockheight;
                }

                y -= lastblockheight;
                incrementblock = lastblockheight;
                lastblockheight = 0;
                x += blockwidth;
            }
        }

        public void writeDataScaled(byte[][,] raster, int componentIndex, BlockUpsamplingMode mode)
        {
            int x = 0, y = 0, lastblockheight = 0, incrementblock = 0;

            int blockIdx = 0;

            int w = raster[0].GetLength(0),
                h = raster[0].GetLength(1);

            // Keep looping through all of the blocks until there are no more.
            while (blockIdx < decodedCount)
            {
                int blockwidth = 0;
                int blockheight = 0;

                if (x >= w) { x = 0; y += incrementblock; }

                // Loop through the horizontal component blocks of the MCU first
                // then for each horizontal line write out all of the vertical
                // components
                for (int factorVIndex = 0; factorVIndex < factorV; factorVIndex++)
                {
                    blockwidth = 0;

                    for (int factorHIndex = 0; factorHIndex < factorH; factorHIndex++)
                    {
                        // Writes the data at the specific X and Y coordinate of this component. Blocks are always 8x8,
                        // so the sizes that used to be read off each array are constants now.
                        writeBlockScaled(raster, decoded, blockIdx * BlockPixels, componentIndex, x, y, mode);
                        blockIdx++;

                        blockwidth += BlockSide * factorUpH;
                        x += BlockSide * factorUpH;
                        blockheight = BlockSide * factorUpV;
                    }

                    y += blockheight;
                    x -= blockwidth;
                    lastblockheight += blockheight;
                }
                y -= lastblockheight;
                incrementblock = lastblockheight;
                lastblockheight = 0;
                x += blockwidth;
            }
        }

        private void writeBlockScaled(byte[][,] raster, byte[] blocks, int blockOffset, int compIndex, int x, int y, BlockUpsamplingMode mode)
        {
            int w = raster[0].GetLength(0),
                h = raster[0].GetLength(1);

            int factorUpVertical = factorUpV,
                factorUpHorizontal = factorUpH;

            int oldV = BlockSide,
                oldH = BlockSide,
                newV = oldV * factorUpVertical,
                newH = oldH * factorUpHorizontal;

            byte[,] comp = raster[compIndex];

            // Blocks may spill over the frame so we bound by the frame size
            int yMax = newV; if (y + yMax > h) yMax = h - y;
            int xMax = newH; if (x + xMax > w) xMax = w - x;

            switch (mode)
            {
                case BlockUpsamplingMode.BoxFilter:

                    #region Upsampling by repeating values

                    // Special case 1: No scale-up
                    if (factorUpVertical == 1 && factorUpHorizontal == 1)
                    {
                        for (int u = 0; u < xMax; u++)
                            for (int v = 0; v < yMax; v++)
                                comp[u + x, y + v] = blocks[blockOffset + v * BlockSide + u];
                    }
                    // Special case 2: Perform scale-up 4 pixels at a time
                    else if (factorUpHorizontal == 2 &&
                             factorUpVertical == 2 &&
                             xMax == newH && yMax == newV)
                    {
                        for (int src_u = 0; src_u < oldH; src_u++)
                        {
                            int bx = src_u * 2 + x;

                            for (int src_v = 0; src_v < oldV; src_v++)
                            {
                                byte val = blocks[blockOffset + src_v * BlockSide + src_u];
                                int by = src_v * 2 + y;

                                comp[bx, by] = val;
                                comp[bx, by + 1] = val;
                                comp[bx + 1, by] = val;
                                comp[bx + 1, by + 1] = val;
                            }
                        }
                    }
                    else
                    {
                        /* Perform scaling (Box filter) */
                        for (int u = 0; u < xMax; u++)
                        {
                            int src_u = u / factorUpHorizontal;
                            for (int v = 0; v < yMax; v++)
                            {
                                int src_v = v / factorUpVertical;
                                comp[u + x, y + v] = blocks[blockOffset + src_v * BlockSide + src_u];
                            }
                        }
                    }


                    #endregion
                    break;

                // JRP 4/7/08 -- This mode is disabled temporarily as it needs to be fixed after
                //               recent performance tweaks.
                //               It can produce slightly better (less blocky) decodings.

                //case BlockUpsamplingMode.Interpolate:
                //    #region Upsampling by interpolation
                //    for (int u = 0; u < newH; u++)
                //    {
                //        for (int v = 0; v < newV; v++)
                //        {
                //            int val = 0;
                //            for (int x = 0; x < factorUpHorizontal; x++)
                //            {
                //                int src_u = (u + x) / factorUpHorizontal;
                //                if (src_u >= oldH) src_u = oldH - 1;
                //                for (int y = 0; y < factorUpVertical; y++)
                //                {
                //                    int src_v = (v + y) / factorUpVertical;
                //                    if (src_v >= oldV) src_v = oldV - 1;
                //                    val += src[src_v, src_u];
                //                }
                //            }
                //            dest[v, u] = (byte)(val / (factorUpHorizontal * factorUpVertical));
                //        }
                //    }
                //    #endregion
                //    break;

                default:
                    throw new ArgumentException("Upsampling mode not supported.");
            }
        }

        internal delegate void DecodeFunction(JPEGBinaryReader jpegReader, float[] zigzagMCU);
        public DecodeFunction Decode;

        public void DecodeBaseline(JPEGBinaryReader stream, float[] dest)
        {
            float dc = decode_dc_coefficient(stream);
            decode_ac_coefficients(stream, dest);
            dest[0] = dc;
        }

        public void DecodeDCFirst(JPEGBinaryReader stream, float[] dest)
        {
            float[] datablock = new float[64];
            int s = DCTable.Decode(stream);
            int r = stream.ReadBits(s);
            s = HuffmanTable.Extend(r, s);
            s = (int)previousDC + s;
            previousDC = s;

            dest[0] = s << successiveLow;
        }

        public void DecodeACFirst(JPEGBinaryReader stream, float[] zz)
        {
            if (stream.eob_run > 0)
            {
                stream.eob_run--;
                return;
            }

            for (int k = spectralStart; k <= spectralEnd; k++)
            {
                int s = ACTable.Decode(stream);
                int r = s >> 4;
                s &= 15;


                if (s != 0)
                {
                    k += r;

                    r = stream.ReadBits(s);
                    s = HuffmanTable.Extend(r, s);
                    zz[k] = s << successiveLow;
                }
                else
                {
                    if (r != 15)
                    {
                        stream.eob_run = 1 << r;

                        if (r != 0)
                            stream.eob_run += stream.ReadBits(r);

                        stream.eob_run--;

                        break;
                    }

                    k += 15;
                }
            }
        }

        public void DecodeDCRefine(JPEGBinaryReader stream, float[] dest)
        {
            if (stream.ReadBits(1) == 1)
            {
                dest[0] = (int)dest[0] | 1 << successiveLow;
            }
        }

        public void DecodeACRefine(JPEGBinaryReader stream, float[] dest)
        {
            int p1 = 1 << successiveLow;
            int m1 = -1 << successiveLow;

            int k = spectralStart;

            if (stream.eob_run == 0)
                for (; k <= spectralEnd; k++)
                {
                    #region Decode and check S

                    int s = ACTable.Decode(stream);
                    int r = s >> 4;
                    s &= 15;

                    if (s != 0)
                    {
                        if (s != 1)
                            throw new Exception("Decode Error");

                        if (stream.ReadBits(1) == 1)
                            s = p1;
                        else
                            s = m1;
                    }
                    else
                    {
                        if (r != 15)
                        {
                            stream.eob_run = 1 << r;

                            if (r > 0)
                                stream.eob_run += stream.ReadBits(r);
                            break;
                        }

                    } // if (s != 0)

                    #endregion

                    // Apply the update
                    do
                    {
                        if (dest[k] != 0)
                        {
                            if (stream.ReadBits(1) == 1)
                            {
                                if (((int)dest[k] & p1) == 0)
                                {
                                    if (dest[k] >= 0)
                                        dest[k] += p1;
                                    else
                                        dest[k] += m1;
                                }
                            }
                        }
                        else
                        {
                            if (--r < 0)
                                break;
                        }

                        k++;

                    } while (k <= spectralEnd);

                    if (s != 0 && k < 64)
                    {
                        dest[k] = s;
                    }
                } // for k = start ... end


            if (stream.eob_run > 0)
            {
                for (; k <= spectralEnd; k++)
                {
                    if (dest[k] != 0)
                    {
                        if (stream.ReadBits(1) == 1)
                        {
                            if (((int)dest[k] & p1) == 0)
                            {
                                if (dest[k] >= 0)
                                    dest[k] += p1;
                                else
                                    dest[k] += m1;
                            }
                        }
                    }
                }

                stream.eob_run--;
            }
        }


        public void SetBlock(int idx)
        {
            if (scanData.Count < idx)
                throw new Exception("Invalid block ID.");

            // expand the data list
            if (scanData.Count == idx)
            {
                scanMCUs = new float[factorH, factorV][];
                for (int i = 0; i < factorH; i++)
                    for (int j = 0; j < factorV; j++)
                        scanMCUs[i, j] = new float[64];

                scanData.Add(scanMCUs);
            }
            else // reference an existing block
            {
                scanMCUs = scanData[idx];
            }
        }

        public void DecodeMCU(JPEGBinaryReader jpegReader, int i, int j)
        {
            Decode(jpegReader, scanMCUs[i, j]);
        }

        /// <summary>
        /// Generated from text on F-22, F.2.2.1 - Huffman decoding of DC
        /// coefficients on ISO DIS 10918-1. Requirements and Guidelines.
        /// </summary>
        /// <param name="JPEGStream">Stream that contains huffman bits</param>
        /// <returns>DC coefficient</returns>
        public float decode_dc_coefficient(JPEGBinaryReader JPEGStream)
        {
            int t = DCTable.Decode(JPEGStream);
            float diff = JPEGStream.ReadBits(t);
            diff = HuffmanTable.Extend((int)diff, t);
            diff = previousDC + diff;
            previousDC = diff;
            return diff;
        }


        /// <summary>
        /// Generated from text on F-23, F.13 - Huffman decoded of AC coefficients
        /// on ISO DIS 10918-1. Requirements and Guidelines.
        /// </summary>
        internal void decode_ac_coefficients(JPEGBinaryReader JPEGStream, float[] zz)
        {
            for (int k = 1; k < 64; k++)
            {
                int s = ACTable.Decode(JPEGStream);
                int r = s >> 4;
                s &= 15;


                if (s != 0)
                {
                    k += r;

                    r = JPEGStream.ReadBits(s);
                    s = HuffmanTable.Extend(r, s);
                    zz[k] = s;
                }
                else
                {
                    if (r != 15)
                    {
                        //throw new JPEGMarkerFoundException();
                        return;
                    }
                    k += 15;
                }
            }
        }
    }

}
