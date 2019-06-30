using Adamantium.Core;
using Adamantium.Engine.Graphics.Imaging.PNG.Chunks;
using Adamantium.Engine.Graphics.Imaging.PNG.IO;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGDecoder
    {
        private PNGStreamReader stream;
        private PNGCompressor compressor;
        public PNGDecoder(PNGStreamReader stream)
        {
            this.stream = stream;
            compressor = new PNGCompressor();
        }

        public Image Decode()
        {
            return Decode(PNGColorType.RGBA, 8);
        }

        private Image Decode(PNGColorType colorType = PNGColorType.RGBA, uint bitDepth = 8)
        {
            PNGState state = new PNGState();
            state.InfoRaw.ColorType = colorType;
            state.InfoRaw.BitDepth = bitDepth;
            return Decode(state);
        }

        private Image Decode(PNGState state)
        {
            var error = DecodeGeneric(state, out var pngImage);

            if (error != 0)
            {
                throw new PNGDecodeException(error);
            }

            if (pngImage.IsMultiFrame)
            {
                return GetMultiFrameImage(pngImage, state);
            }
            return GetSingleFrameImage(pngImage, state);

            // TODO: should move these code to another place
            ////Premultiply alpha for formats, which does not support tansparency
            //for (int i = 0; i < rawBuffer.Length; i+=4)
            //{
            //    float alpha = rawBuffer[i + 3] / 255.0f;

            //    rawBuffer[i] = (byte)(((rawBuffer[i]/255.0f) * alpha) * 255);
            //    rawBuffer[i + 1] = (byte)(((rawBuffer[i+1] / 255.0f) * alpha) * 255);
            //    rawBuffer[i + 2] = (byte)(((rawBuffer[i+2] / 255.0f) * alpha) * 255);
            //}
        }

        private Image GetSingleFrameImage(PNGImage pngImage, PNGState state)
        {
            var frame = pngImage.Frames[0];
            frame.Width = (uint)pngImage.Header.Width;
            frame.Height = (uint)pngImage.Header.Height;
            ConvertColorsIfNeeded(frame, state);
            var descr = GetImageDescription(state, pngImage.Header.Width, pngImage.Header.Height);
            var img = Image.New(descr);
            var handle = GCHandle.Alloc(frame.RawPixelBuffer, GCHandleType.Pinned);
            Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), frame.RawPixelBuffer.Length);
            handle.Free();

            return img;
        }

        private Image GetMultiFrameImage(PNGImage pngImage, PNGState state)
        {
            for (int i = 0; i < pngImage.Frames.Count; i++)
            {
                PNGFrame frame = pngImage.Frames[i];
                var bytesPerPixel = (int)(PNGColorConvertion.GetBitsPerPixel(state.InfoRaw) / 8);
                if (bytesPerPixel == 0)
                {
                    bytesPerPixel = 1;
                }

                ConvertColorsIfNeeded(frame, state);

                if (i > 0)
                {
                    var baseFrame = pngImage.Frames[i - 1];
                    byte[] pixels = new byte[baseFrame.RawPixelBuffer.Length];
                    if (frame.DisposeOp == DisposeOp.None)
                    {
                        Array.Copy(baseFrame.RawPixelBuffer, pixels, pixels.Length);
                    }
                    int lineLength = pngImage.Header.Width * bytesPerPixel;

                    for (int k = 0; k < frame.Height; ++k)
                    {
                        var dstIndex = ((frame.YOffset + k) * lineLength) + (frame.XOffset * bytesPerPixel);
                        var srcIndex = k * frame.Width * bytesPerPixel;

                        if (frame.BlendOp == BlendOp.Over)
                        {
                            var basePixelBuffer = baseFrame.RawPixelBuffer;
                            var pixelBuffer = frame.RawPixelBuffer;
                            // output = alpha * foreground + (1-alpha) * background for each color channel 
                            // where the alpha value and the input and output sample values are expressed as fractions in the range 0 to 1
                            int offset = 0;
                            for (var n = srcIndex; n < pixelBuffer.Length; n+=4)
                            {
                                var alpha = pixelBuffer[n + 3] / 255.0f;
                                var baseAlpha = basePixelBuffer[dstIndex + 3] / 255.0f;
                                pixelBuffer[n + 0] = (byte)(alpha * (pixelBuffer[n + 0] / 255.0f) + (1 - baseAlpha) * (basePixelBuffer[dstIndex + offset + 0] / 255.0f) * 255);
                                pixelBuffer[n + 1] = (byte)(alpha * (pixelBuffer[n + 1] / 255.0f) + (1 - baseAlpha) * (basePixelBuffer[dstIndex + offset + 1] / 255.0f) * 255);
                                pixelBuffer[n + 2] = (byte)(alpha * (pixelBuffer[n + 2] / 255.0f) + (1 - baseAlpha) * (basePixelBuffer[dstIndex + offset + 2] / 255.0f) * 255);
                                offset += 4;
                            }
                        }

                        Array.Copy(frame.RawPixelBuffer, srcIndex, pixels, dstIndex, frame.Width * bytesPerPixel);
                    }
                    frame.Width = (uint)pngImage.Header.Width;
                    frame.Height = (uint)pngImage.Header.Height;
                    frame.XOffset = 0;
                    frame.YOffset = 0;
                    frame.RawPixelBuffer = pixels;

                }
            }

            var img = Image.New3D(pngImage.Header.Width, pngImage.Header.Height, pngImage.Frames.Count, new MipMapCount(1),SurfaceFormat.R8G8B8A8.UNorm);
            for (int i = 0; i < img.PixelBuffer.Count; ++i)
            {
                var frame = pngImage.Frames[i];
                var handle = GCHandle.Alloc(frame.RawPixelBuffer, GCHandleType.Pinned);
                Utilities.CopyMemory(img.PixelBuffer[i].DataPointer, handle.AddrOfPinnedObject(), pngImage.Frames[i].RawPixelBuffer.Length);
                img.PixelBuffer[i].DelayNumerator = frame.DelayNumerator;
                img.PixelBuffer[i].DelayDenominator = frame.DelayDenominator;
                handle.Free();
            }

            //int x = 1;
            //descr.Width = (int)pngImage.Frames[x].Width;
            //descr.Height = (int)pngImage.Frames[x].Height;
            //var img = Image.New(descr);
            //var handle = GCHandle.Alloc(pngImage.Frames[x].RawPixelBuffer, GCHandleType.Pinned);
            //Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), pngImage.Frames[x].RawPixelBuffer.Length);
            //handle.Free();

            return img;
        }

        private ImageDescription GetImageDescription(PNGState state, int width, int height)
        {
            var bitsPerPixel = PNGColorConvertion.GetBitsPerPixel(state.InfoRaw);
            ImageDescription descr = new ImageDescription();
            descr.Width = width;
            descr.Height = height;
            descr.ArraySize = 1;
            descr.MipLevels = 1;
            descr.Depth = 1;
            descr.Dimension = TextureDimension.Texture2D;
            if (bitsPerPixel == 8)
            {
                descr.Format = AdamantiumVulkan.Core.Format.R8_UNORM;
            }
            else if (bitsPerPixel == 24)
            {
                descr.Format = AdamantiumVulkan.Core.Format.R8G8B8_UNORM;
            }
            else if (bitsPerPixel == 32)
            {
                descr.Format = AdamantiumVulkan.Core.Format.R8G8B8A8_UNORM;
            }

            return descr;
        }

        private void ConvertColorsIfNeeded(PNGFrame frame, PNGState state)
        {
            if (!state.DecoderSettings.ColorСonvert || state.InfoRaw == state.InfoPng.ColorMode)
            {
                /*same color type, no copying or converting of data needed*/
                /*store the info_png color settings on the info_raw so that the info_raw still reflects what colortype
                the raw image has to the end user*/
                if (!state.DecoderSettings.ColorСonvert)
                {
                    state.InfoRaw = state.InfoPng.ColorMode;
                }
            }
            else
            {
                /*color conversion needed; sort of copy of the data*/
                if (!(state.InfoRaw.ColorType == PNGColorType.RGB || state.InfoRaw.ColorType == PNGColorType.RGBA)
                    && state.InfoRaw.BitDepth != 8)
                {
                    /*unsupported color mode conversion*/
                    throw new PNGDecodeException(56);
                }

                var width = (int)frame.Width;
                var height = (int)frame.Height;

                int rawBufferSize = (int)GetRawSizeLct(width, height, state.InfoRaw);
                var outBuffer = new byte[rawBufferSize];

                state.Error = PNGColorConvertion.Convert(outBuffer, frame.RawPixelBuffer, state.InfoRaw, state.InfoPng.ColorMode, width, height);
                frame.RawPixelBuffer = outBuffer;
                if (state.Error > 0)
                {
                    throw new PNGDecodeException(state.Error);
                }
            }
        }

        private uint DecodeGeneric(PNGState state, out PNGImage pngImage)
        {
            bool IEND = false;
            string chunk;
            ulong i;
            /*the data from idat chunks*/
            long predict = 0;

            // initialize out parameters in case of errors
            pngImage = new PNGImage();

            pngImage.Header = ReadHeaderChunk(state);
            if (state.Error != 0)
            {
                throw new PNGDecodeException(state.Error);
            }

            if (CheckPixelOverflow(pngImage.Header.Width, pngImage.Header.Height, state.InfoPng.ColorMode, state.InfoRaw))
            {
                /*overflow possible due to amount of pixels*/
                state.Error = 92;
            }

            stream.Position = 33;

            long currentPosition = stream.Position;
            PNGFrame currentFrame = null;

            while (!IEND && state.Error == 0)
            {
                /*error: size of the in buffer too small to contain next chunk*/
                if ((stream.Position+12)> stream.Length )
                {
                    if (state.DecoderSettings.IgnoreEnd)
                    {
                        /*other errors may still happen though*/
                        break;
                    }
                    state.Error = 30;
                }

                /*length of the data of the chunk, excluding the length bytes, chunk type and CRC bytes*/
                uint chunkSize = stream.ReadChunkSize();

                if (chunkSize > int.MaxValue)
                {
                    if (state.DecoderSettings.IgnoreEnd)
                    {
                        /*other errors may still happen though*/
                        break;
                    }
                    state.Error = 63;
                }

                if ((stream.Position + chunkSize + 12) > stream.Length)
                {
                    /*error: size of the in buffer too small to contain next chunk*/
                    state.Error = 64;
                }

                var chunkType = stream.ReadChunkType();

                var pos = stream.Position - 4;
                uint crc = 0;

                /*
                    If the default image is the first frame:

                    Sequence number    Chunk
                    (none)             `acTL`
                    0                  `fcTL` first frame
                    (none)             `IDAT` first frame / default image
                    1                  `fcTL` second frame
                    2                  first `fdAT` for second frame
                    3                  second `fdAT` for second frame
                    ....

                    If the default image is not part of the animation:
                                    Sequence number    Chunk
                    (none)             `acTL`
                    (none)             `IDAT` default image
                    0                  `fcTL` first frame
                    1                  first `fdAT` for first frame
                    2                  second `fdAT` for first frame
                    ....
                */
                bool isPartOfAnimation = false;
                switch (chunkType)
                {
                    case "acTL":
                        var actl = stream.ReadacTL(state);
                        pngImage.FramesCount = actl.FramesCount;
                        pngImage.RepeatCout = actl.RepeatCout;
                        break;
                    case "fcTL":
                        currentFrame = new PNGFrame();
                        isPartOfAnimation = true;
                        pngImage.Frames.Add(currentFrame);
                        stream.ReadfcTL(state, currentFrame);
                        break;
                    case "fdAT":
                        currentFrame.SequenceNumberFDAT = stream.ReadUInt32();
                        currentFrame.FrameData = stream.ReadBytes((int)chunkSize - 4);
                        crc = stream.ReadUInt32();
                        stream.Position = pos;
                        var data = stream.ReadBytes(currentFrame.FrameData.Length + 8);
                        var checksum = CRC32.CalculateCheckSum(data);
                        if (crc != checksum && !state.DecoderSettings.IgnoreCrc)
                        {
                            state.Error = 57; // checksum mismatch;
                        }
                        break;
                    case "IDAT":
                        if (currentFrame == null && !isPartOfAnimation)
                        {
                            currentFrame = new PNGFrame();
                            pngImage.DefaultImage = currentFrame;
                            pngImage.Frames.Add(currentFrame);
                        }
                        var bytes = stream.ReadBytes((int)chunkSize);
                        crc = stream.ReadUInt32();
                        stream.Position = pos;
                        var crcData = stream.ReadBytes(bytes.Length + 4);
                        checksum = CRC32.CalculateCheckSum(crcData);
                        if (crc != checksum && !state.DecoderSettings.IgnoreCrc)
                        {
                            state.Error = 57; // checksum mismatch;
                        }
                        currentFrame.AddBytes(bytes);
                        break;
                    case "IEND":
                        IEND = true;
                        break;
                    case "PLTE":
                        ReadPLTEChunk(state, chunkSize);
                        break;
                    case "tRNS":
                        ReadtRNSChunk(state, chunkSize);
                        break;
                    case "bkGD":
                        ReadbKGDChunk(state, chunkSize);
                        break;
                    case "tEXt":
                        ReadtEXtChunk(state, chunkSize);
                        break;
                    case "zTXt":
                        ReadzTXtChunk(state, this, chunkSize);
                        break;
                    case "iTXt":
                        ReadiTXtChunk(state, this, chunkSize);
                        break;
                    case "tIME":
                        ReadtIMEChunk(state);
                        break;
                    case "pHYs":
                        ReadpHYsChunk(state.InfoPng);
                        break;
                    case "gAMA":
                        ReadgAMAChunk(state.InfoPng);
                        break;
                    case "cHRM":
                        ReadcHRMChunk(state, chunkSize);
                        break;
                    case "sRGB":
                        ReadsRGBChunk(state.InfoPng);
                        break;
                    case "iCCP":
                        ReadiCCPChunk(state, chunkSize);
                        break;
                    /*it's not an implemented chunk type, so ignore it: skip over the data*/
                    default:
                        /*error: unknown critical chunk (5th bit of first byte of chunk type is 0)*/
                        if (!state.DecoderSettings.IgnoreCritical)
                        {
                            state.Error = 69;
                        }
                        break;
                }

                if (!IEND)
                {
                    currentPosition += chunkSize + 12;
                    stream.Position = currentPosition;
                }
            }

            foreach (var frame in pngImage.Frames)
            {
                int width = frame.Width != 0 ? (int)frame.Width : pngImage.Header.Width;
                int height = frame.Height != 0 ? (int)frame.Height : pngImage.Header.Height;
                predict = 0;
                if (state.InfoPng.InterlaceMethod == InterlaceMethod.None)
                {
                    predict = GetRawSizeIdat(width, height, state.InfoPng.ColorMode);
                }
                else
                {
                    /*Adam-7 interlaced: predicted size is the sum of the 7 sub-images sizes*/
                    var colorMode = state.InfoPng.ColorMode;
                    predict += GetRawSizeIdat((width + 7) >> 3, (height + 7) >> 3, colorMode);
                    if (width > 4)
                    {
                        predict += GetRawSizeIdat((width + 3) >> 3, (height + 7) >> 3, colorMode);
                    }
                    predict += GetRawSizeIdat((width + 3) >> 2, (height + 3) >> 3, colorMode);
                    if (width > 2)
                    {
                        predict += GetRawSizeIdat((width + 1) >> 2, (height + 3) >> 2, colorMode);
                    }
                    predict += GetRawSizeIdat((width + 1) >> 1, (height + 1) >> 2, colorMode);
                    if (width > 1)
                    {
                        predict += GetRawSizeIdat((width) >> 1, (height + 1) >> 1, colorMode);
                    }
                    predict += GetRawSizeIdat((width), (height) >> 1, colorMode);
                }

                var scanlines = new List<byte>((int)predict);

                state.Error = compressor.Decompress(frame.FrameData, state.DecoderSettings, scanlines);

                long bufferSize = GetRawSizeLct(width, height, state.InfoPng.ColorMode);
                frame.RawPixelBuffer = new byte[bufferSize];

                if (state.Error == 0)
                {
                    state.Error = PostProcessScanline(frame.RawPixelBuffer, scanlines.ToArray(), width, height, state.InfoPng);
                }

                if (state.Error > 0)
                {
                    break;
                }
            }

            return state.Error;
        }

        private void ReadPLTEChunk(PNGState state, uint chunkSize)
        {
            stream.ReadPLTE(state, chunkSize);
        }

        private void ReadtRNSChunk(PNGState state, uint chunkSize)
        {
            stream.ReadtRNS(state, chunkSize);
        }

        private void ReadbKGDChunk(PNGState state, uint chunkSize)
        {
            stream.ReadbKGD(state, chunkSize);
        }

        private void ReadzTXtChunk(PNGState state, PNGDecoder pNGDecoder, uint chunkSize)
        {
            stream.ReadzTXt(state, pNGDecoder, chunkSize);
        }

        private void ReadtIMEChunk(PNGState state)
        {
            stream.ReadtIME(state);
        }

        private void ReadiTXtChunk(PNGState state, PNGDecoder pNGDecoder, uint chunkSize)
        {
            stream.ReadiTXt(state, pNGDecoder, chunkSize);
        }

        private void ReadcHRMChunk(PNGState state, uint chunkSize)
        {
            if (chunkSize != 32)
            {
                /*invalid cHRM chunk size*/
                state.Error = 97;
                return;
            }
            state.InfoPng.cHRM = stream.ReadcHRM();
        }

        

        private unsafe uint PostProcessScanline(byte[] rawBuffer, byte[] inputData, int width, int height, PNGInfo infoPng)
        {
            /*
            This function converts the filtered-padded-interlaced data into pure 2D image buffer with the PNG's colortype.
            Steps:
            *) if no Adam7: 1) unfilter 2) remove padding bits (= posible extra bits per scanline if bpp < 8)
            *) if adam7: 1) 7x unfilter 2) 7x remove padding bits 3) Adam7_deinterlace
            NOTE: the in buffer will be overwritten with intermediate data!
            */
            uint error = 0;
            var bpp = PNGColorConvertion.GetBitsPerPixel(infoPng.ColorMode);
            if (bpp == 0)
            {
                /*error: invalid colortype*/
                return 31;
            }

            if (infoPng.InterlaceMethod == 0)
            {
                fixed (byte* inPtr = &inputData[0])
                {
                    fixed (byte* rawPtr = &rawBuffer[0])
                    {
                        if (bpp < 8 && width * bpp != ((width * bpp + 7) / 8) * 8)
                        {
                            error = PNGFilter.Unfilter(inPtr, inPtr, width, height, (int)bpp);
                            if (error > 0)
                            {
                                return error;
                            }
                            RemovePaddingBits(rawPtr, inPtr, (uint)(width * bpp), (uint)((width * bpp + 7) / 8) * 8, (uint)height);
                        }
                        else
                        {
                            error = PNGFilter.Unfilter(rawPtr, inPtr, width, height, (int)bpp);
                        }
                    }
                }
            }
            else /*interlace_method is 1 (Adam7)*/
            {
                uint[] passWidth = new uint[7];
                uint[] passHeight = new uint[7];
                uint[] filterPassStart = new uint[8];
                uint[] paddedPassStart = new uint[8];
                uint[] passStart = new uint[8];

                Adam7.GetPassValues(passWidth, passHeight, filterPassStart, paddedPassStart, passStart, (uint)width, (uint)height, bpp);

                for (int i = 0; i != 7; ++i)
                {
                    fixed (byte* rawPtr = &inputData[paddedPassStart[i]])
                    {
                        fixed (byte* inPtr = &inputData[filterPassStart[i]])
                        {
                            error = PNGFilter.Unfilter(rawPtr, inPtr, (int)passWidth[i], (int)passHeight[i], (int)bpp);
                        }
                    }

                    /*TODO: possible efficiency improvement: if in this reduced image the bits fit nicely in 1 scanline,
                    move bytes instead of bits or move not at all*/
                    if (bpp < 8)
                    {
                        /*remove padding bits in scanlines; after this there still may be padding
                        bits between the different reduced images: each reduced image still starts nicely at a byte*/
                        fixed (byte* rawPtr = &inputData[passStart[i]])
                        {
                            fixed (byte* inPtr = &inputData[paddedPassStart[i]])
                            {
                                RemovePaddingBits(rawPtr, inPtr, passWidth[i] * bpp, 
                                    ((passWidth[i] * bpp + 7) / 8) * 8, passHeight[i]);
                            }
                        }
                    }
                }

                Adam7.Deinterlace(rawBuffer, inputData, (uint)width, (uint)height, bpp);
            }

            return error;
        }

        private unsafe void RemovePaddingBits(byte* rawBuffer, byte* inputData, uint olinebits, uint ilinebits, uint height)
        {
            /*
            After filtering there are still padding bits if scanlines have non multiple of 8 bit amounts. They need
            to be removed (except at last scanline of (Adam7-reduced) image) before working with pure image buffers
            for the Adam7 code, the color convert code and the output to the user.
            in and out are allowed to be the same buffer, in may also be higher but still overlapping; in must
            have >= ilinebits*h bits, out must have >= olinebits*h bits, olinebits must be <= ilinebits
            also used to move bits after earlier such operations happened, e.g. in a sequence of reduced images from Adam7
            only useful if (ilinebits - olinebits) is a value in the range 1..7
            */
            uint diff = ilinebits - olinebits;
            /*input and output bit pointers*/
            int ibp = 0;
            int obp = 0;
            for (int i = 0; i < height; ++i)
            {
                for (int x = 0; x < olinebits; ++x)
                {
                    byte bit = BitHelper.ReadBitFromReversedStream(ref ibp, inputData);
                    BitHelper.SetBitOfReversedStream(ref obp, rawBuffer, bit);
                }

                ibp += (int)diff;
            }
        }

        

        /*in an idat chunk, each scanline is a multiple of 8 bits, unlike the lodepng output buffer,
        and in addition has one extra byte per line: the filter byte. So this gives a larger
        result than lodepng_get_raw_size. */
        private long GetRawSizeIdat(int width, int height, PNGColorMode colorMode)
        {
            var bpp = PNGColorConvertion.GetBitsPerPixel(colorMode);
            /* + 1 for the filter byte, and possibly plus padding bits per line */
            var line = ((width / 8) * bpp) + 1 + ((width & 7) * bpp + 7) / 8;
            return height * line;
        }

        internal static long GetRawSizeLct(int width, int height, PNGColorMode colorMode)
        {
            var bpp = PNGColorConvertion.GetBitsPerPixel(colorMode);
            var n = width * height;
            return ((n / 8) * bpp) + ((n & 7) * bpp + 7) / 8;
        }

        private void ReadtEXtChunk(PNGState state, uint chunkLength)
        {
            if (!state.DecoderSettings.ReadTextChunks)
            {
                return;
            }
            var text = stream.ReadtEXt(state, chunkLength);
            var textItem = new TXTItem();
            textItem.Key = text.Key;
            textItem.Text = text.Text;
            state.InfoPng.TextItems.Add(textItem);
        }

        /*reads header and resets other parameters in state->info_png*/
        private IHDR ReadHeaderChunk(PNGState state)
        {
            var info = state.InfoPng;

            if (stream.Length == 0)
            {
                /*error: the given data is empty*/
                state.Error = 48;
            }

            if (stream.Length < 33)
            {
                /*error: the data length is smaller than the length of a PNG header*/
                state.Error = 27;
            }

            if (!stream.ReadPNGSignature())
            {
                /*error: the first 8 bytes are not the correct PNG signature*/
                state.Error = 28;
            }

            if (stream.ReadInt32() != 13)
            {
                /*error: header size must be 13 bytes*/
                state.Error = 94;
            }

            //stream.Position -= 4;

            if (stream.ReadChunkType() != "IHDR")
            {
                /*error: it doesn't start with a IHDR chunk!*/
                state.Error = 29;
            }
            var header = stream.ReadIHDR();

            info.ColorMode.BitDepth = header.BitDepth;
            info.ColorMode.ColorType = header.ColorType;
            info.CompressionMethod = header.CompressionMethod;
            info.FilterMethod = header.FilterMethod;
            info.InterlaceMethod = header.InterlaceMethod;

            if (!state.DecoderSettings.IgnoreCrc)
            {
                if (header.CRC != header.CheckSum)
                {
                    /*invalid CRC*/
                    state.Error = 57;
                }
            }

            /*error: only compression method 0 is allowed in the specification*/
            if (info.CompressionMethod != 0) state.Error = 32;
            /*error: only filter method 0 is allowed in the specification*/
            if (info.FilterMethod != 0) state.Error = 33;

            state.Error = CheckColorValidity(info.ColorMode.ColorType, info.ColorMode.BitDepth);

            return header;
        }

        private void ReadiCCPChunk(PNGState state, uint chunkSize)
        {
            var iccp = stream.ReadiCCP(state, this, chunkSize);
        }

        private void ReadsRGBChunk(PNGInfo info)
        {
            var srgb = stream.ReadsRGB();
            info.IsSrgbDefined = true;
            info.SrgbIntent = srgb.RenderingIntent;
        }

        private void ReadgAMAChunk(PNGInfo info)
        {
            var gama = stream.ReadgAMA();
            info.IsGamaDefined = true;
            info.Gamma = gama.Gamma;
        }

        private void ReadpHYsChunk(PNGInfo info)
        {
            var phys = stream.ReadpHYs();
            info.PhysX = phys.PhysX;
            info.PhysY = phys.PhysY;
            info.PhysUnit = phys.Unit;
            info.IsPhysDefined = true;
        }

        private uint CheckColorValidity(PNGColorType colorType, uint bitDepth)
        {
            switch(colorType)
            {
                case PNGColorType.Grey:
                    if (!(bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8|| bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                case PNGColorType.GreyAlpha:
                case PNGColorType.RGB:
                case PNGColorType.RGBA:
                    if (!(bitDepth == 8 || bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                case PNGColorType.Palette:
                    if (!(bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8))
                    {
                        return 37;
                    }
                    break;
                default:
                    return 31;
            }
            return 0;
        }

        /*Safely checks whether size_t overflow can be caused due to amount of pixels.
        This check is overcautious rather than precise. If this check indicates no overflow,
        you can safely compute in a size_t (but not an unsigned):
            -(size_t)w * (size_t)h * 8
            -amount of bytes in IDAT (including filter, padding and Adam7 bytes)
            -amount of bytes in raw color model
        Returns true if overflow possible, false if not.
        */
        private bool CheckPixelOverflow(int width, int height, PNGColorMode pngColor, PNGColorMode rawColor)
        {
            ulong bpp = Math.Max(PNGColorConvertion.GetBitsPerPixel(pngColor), PNGColorConvertion.GetBitsPerPixel(rawColor));
            ulong numPixels, total;
            ulong line; // bytes per line in worst case

            if (MulOverflow((ulong)width, (ulong)height, out numPixels))
            {
                return true;
            }

            /* bit pointer with 8-bit color, or 8 bytes per channel color */
            if (MulOverflow(numPixels, 8, out total))
            {
                return true;
            }

            /* Bytes per scanline with the expression "(width / 8) * bpp) + ((width & 7) * bpp + 7) / 8" */
            if (MulOverflow((ulong)(width / 8), bpp, out line))
            {
                return true;
            }

            if (AddOverflow(line, ((ulong)(width & 7) * bpp +7) / 8, out line))
            {
                return true;
            }

            /* 5 bytes overhead per line: 1 filterbyte, 4 for Adam7 worst case */
            if (AddOverflow(line, 5, out line))
            {
                return true;
            }

            /* Total bytes in worst case */
            if (AddOverflow(line, (ulong)height, out total))
            {
                return true;
            }

            return false;
        }

        /* Safely check if multiplying two integers will overflow (no undefined
        behavior, compiler removing the code, etc...) and output result. */
        private bool MulOverflow(ulong a, ulong b, out ulong result)
        {
            result = a * b;
            return (a != 0 && result / a != b);
        }

        /* Safely check if adding two integers will overflow (no undefined
        behavior, compiler removing the code, etc...) and output result. */
        private bool AddOverflow(ulong a, ulong b, out ulong result)
        {
            result = a + b;
            return result < a;
        }
    }
}
