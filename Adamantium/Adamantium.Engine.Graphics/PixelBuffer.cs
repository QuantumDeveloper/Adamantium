// Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using Adamantium.Core;
using Adamantium.Engine.Graphics.Imaging;
using Adamantium.Engine.Graphics.Imaging.JPEG;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using System;
using System.IO;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// An unmanaged buffer of pixels.
    /// </summary>
    public sealed class PixelBuffer
    {
        private readonly int width;

        private readonly int height;

        private Format format;

        private readonly int rowStride;

        private readonly int bufferStride;

        private readonly IntPtr dataPointer;

        private readonly int pixelSize;

        /// <summary>
        /// True when RowStride == sizeof(pixelformat) * width
        /// </summary>
        private readonly bool isStrictRowStride;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelBuffer" /> struct.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="format">The format.</param>
        /// <param name="rowStride">The row pitch.</param>
        /// <param name="bufferStride">The slice pitch.</param>
        /// <param name="dataPointer">The pixels.</param>
        public PixelBuffer(int width, int height, Format format, int rowStride, int bufferStride, IntPtr dataPointer)
        {
            if (dataPointer == IntPtr.Zero)
                throw new ArgumentException("Pointer cannot be equal to IntPtr.Zero", nameof(dataPointer));

            this.width = width;
            this.height = height;
            this.format = format;
            this.rowStride = rowStride;
            this.bufferStride = bufferStride;
            this.dataPointer = dataPointer;
            pixelSize = format.SizeOfInBytes();
            isStrictRowStride = (pixelSize * width) == rowStride;
        }

        /// <summary>
        /// Gets the width.
        /// </summary>
        /// <value>The width.</value>
        public int Width => width;

        /// <summary>
        /// Gets the height.
        /// </summary>
        /// <value>The height.</value>
        public int Height => height;

        /// <summary>
        /// Gets the format (this value can be changed)
        /// </summary>
        /// <value>The format.</value>
        public Format Format
        {
            get
            {
                return format;
            }
            set
            {
                if (PixelSize != (int)FormatHelper.SizeOfInBytes(value))
                {
                    throw new ArgumentException(
                       $"Format [{value}] doesn't have same pixel size in bytes than current format [{format}]");
                }
                format = value;
            }
        }

        /// <summary>
        /// Gets the pixel size in bytes.
        /// </summary>
        /// <value>The pixel size in bytes.</value>
        public int PixelSize => pixelSize;

        /// <summary>
        /// Gets the row stride in number of bytes.
        /// </summary>
        /// <value>The row stride in number of bytes.</value>
        public int RowStride => rowStride;

        /// <summary>
        /// Gets the total size in bytes of this pixel buffer.
        /// </summary>
        /// <value>The size in bytes of the pixel buffer.</value>
        public int BufferStride => bufferStride;

        /// <summary>
        /// Gets the pointer to the pixel buffer.
        /// </summary>
        /// <value>The pointer to the pixel buffer.</value>
        public IntPtr DataPointer => dataPointer;

        /// <summary>
        /// Copies this pixel buffer to a destination pixel buffer.
        /// </summary>
        /// <param name="pixelBuffer">The destination pixel buffer.</param>
        /// <remarks>
        /// The destination pixel buffer must have exactly the same dimensions (width, height) and format than this instance.
        /// Destination buffer can have different row stride.
        /// </remarks>
        public unsafe void CopyTo(PixelBuffer pixelBuffer)
        {
            // Check that buffers are identical
            if (this.Width != pixelBuffer.Width
                || this.Height != pixelBuffer.Height
                || PixelSize != FormatHelper.SizeOfInBytes(pixelBuffer.Format))
            {
                throw new ArgumentException("Invalid destination pixelBufferArray. Mush have same Width, Height and Format", nameof(pixelBuffer));
            }

            // If buffers have same size, than we can copy it directly
            if (this.BufferStride == pixelBuffer.BufferStride)
            {
                Utilities.CopyMemory(pixelBuffer.DataPointer, DataPointer, BufferStride);
            }
            else
            {
                var srcPointer = (byte*)this.DataPointer;
                var dstPointer = (byte*)pixelBuffer.DataPointer;
                var rowStride = Math.Min(RowStride, pixelBuffer.RowStride);

                // Copy per scanline
                for (int i = 0; i < Height; i++)
                {
                    Utilities.CopyMemory(new IntPtr(dstPointer), new IntPtr(srcPointer), rowStride);
                    srcPointer += this.RowStride;
                    dstPointer += pixelBuffer.RowStride;
                }
            }
        }

        /// <summary>
        /// Saves this pixel buffer to a file.
        /// </summary>
        /// <param name="fileName">The destination file.</param>
        /// <param name="fileType">Specify the output format.</param>
        /// <remarks>This method support the following format: <c>dds, bmp, jpg, png, gif, tiff, wmp, tga</c>.</remarks>
        public void Save(string fileName, ImageFileType fileType)
        {
            using (var imageStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                Save(imageStream, fileType);
            }
        }

        /// <summary>
        /// Saves this pixel buffer to a stream.
        /// </summary>
        /// <param name="imageStream">The destination stream.</param>
        /// <param name="fileType">Specify the output format.</param>
        /// <remarks>This method support the following format: <c>dds, bmp, jpg, png, gif, tiff, wmp, tga</c>.</remarks>
        public void Save(Stream imageStream, ImageFileType fileType)
        {
            var description = new ImageDescription()
            {
                Width = this.width,
                Height = this.height,
                Depth = 1,
                ArraySize = 1,
                Dimension = TextureDimension.Texture2D,
                Format = this.format,
                MipLevels = 1,
            };
            Image.Save(new[] { this }, 1, description, imageStream, fileType);
        }

        /// <summary>
        /// Gets the pixel value at a specified position.
        /// </summary>
        /// <typeparam name="T">Type of the pixel data</typeparam>
        /// <param name="x">The x-coordinate.</param>
        /// <param name="y">The y-coordinate.</param>
        /// <returns>The pixel value.</returns>
        /// <remarks>
        /// Caution, this method doesn't check bounding.
        /// </remarks>
        public unsafe T GetPixel<T>(int x, int y) where T : struct
        {
            return Utilities.Read<T>(new IntPtr(((byte*)DataPointer + RowStride * y + x * PixelSize)));
        }

        /// <summary>
        /// Gets the pixel value at a specified position.
        /// </summary>
        /// <typeparam name="T">Type of the pixel data</typeparam>
        /// <param name="x">The x-coordinate.</param>
        /// <param name="y">The y-coordinate.</param>
        /// <param name="value">The pixel value.</param>
        /// <remarks>
        /// Caution, this method doesn't check bounding.
        /// </remarks>
        public unsafe void SetPixel<T>(int x, int y, T value) where T : struct
        {
            Utilities.Write(new IntPtr((byte*)DataPointer + RowStride * y + x * PixelSize), ref value);
        }

        /// <summary>
        /// Gets scanline pixels from the buffer.
        /// </summary>
        /// <typeparam name="T">Type of the pixel data</typeparam>
        /// <param name="yOffset">The y line offset.</param>
        /// <returns>Scanline pixels from the buffer</returns>
        /// <exception cref="System.ArgumentException">If the sizeof(T) is an invalid size</exception>
        /// <remarks>
        /// This method is working on a row basis. The <paramref name="yOffset"/> is specifying the first row to get 
        /// the pixels from.
        /// </remarks>
        public T[] GetPixels<T>(int yOffset = 0) where T : struct
        {
            var sizeOfOutputPixel = Utilities.SizeOf<T>();
            var totalSize = Width * Height * pixelSize;
            if ((totalSize % sizeOfOutputPixel) != 0)
                throw new ArgumentException($"Invalid sizeof(T), not a multiple of current size [{totalSize}]in bytes ");

            var buffer = new T[totalSize / sizeOfOutputPixel];
            GetPixels(buffer, yOffset);
            return buffer;
        }

        /// <summary>
        /// Gets scanline pixels from the buffer.
        /// </summary>
        /// <typeparam name="T">Type of the pixel data</typeparam>
        /// <param name="pixels">An allocated scanline pixel buffer</param>
        /// <param name="yOffset">The y line offset.</param>
        /// <returns>Scanline pixels from the buffer</returns>
        /// <exception cref="System.ArgumentException">If the sizeof(T) is an invalid size</exception>
        /// <remarks>
        /// This method is working on a row basis. The <paramref name="yOffset"/> is specifying the first row to get 
        /// the pixels from.
        /// </remarks>
        public void GetPixels<T>(T[] pixels, int yOffset = 0) where T : struct
        {
            GetPixels(pixels, yOffset, 0, pixels.Length);
        }

        /// <summary>
        /// Gets scanline pixels from the buffer.
        /// </summary>
        /// <typeparam name="T">Type of the pixel data</typeparam>
        /// <param name="pixels">An allocated scanline pixel buffer</param>
        /// <param name="yOffset">The y line offset.</param>
        /// <param name="pixelIndex">Offset into the destination <paramref name="pixels"/> buffer.</param>
        /// <param name="pixelCount">Number of pixels to write into the destination <paramref name="pixels"/> buffer.</param>
        /// <exception cref="System.ArgumentException">If the sizeof(T) is an invalid size</exception>
        /// <remarks>
        /// This method is working on a row basis. The <paramref name="yOffset"/> is specifying the first row to get 
        /// the pixels from.
        /// </remarks>
        public unsafe void GetPixels<T>(T[] pixels, int yOffset, int pixelIndex, int pixelCount) where T : struct
        {
            var pixelPointer = (byte*)this.DataPointer + yOffset * rowStride;
            if (isStrictRowStride)
            {
                Utilities.Read(new IntPtr(pixelPointer), pixels, 0, pixelCount);
            }
            else
            {
                var sizeOfOutputPixel = Utilities.SizeOf<T>() * pixelCount;
                var sizePerWidth = sizeOfOutputPixel / Width;
                var remainingPixels = sizeOfOutputPixel % Width;
                for (int i = 0; i < sizePerWidth; i++)
                {
                    Utilities.Read(new IntPtr(pixelPointer), pixels, pixelIndex, Width);
                    pixelPointer += rowStride;
                    pixelIndex += Width;
                }
                if (remainingPixels > 0)
                {
                    Utilities.Read(new IntPtr(pixelPointer), pixels, pixelIndex, remainingPixels);
                }
            }
        }

        /// <summary>
        /// Sets scanline pixels to the buffer.
        /// </summary>
        /// <typeparam name="T">Type of the pixel data</typeparam>
        /// <param name="sourcePixels">Source pixel buffer</param>
        /// <param name="yOffset">The y line offset.</param>
        /// <exception cref="System.ArgumentException">If the sizeof(T) is an invalid size</exception>
        /// <remarks>
        /// This method is working on a row basis. The <paramref name="yOffset"/> is specifying the first row to get 
        /// the pixels from.
        /// </remarks>
        public void SetPixels<T>(T[] sourcePixels, int yOffset = 0) where T : struct
        {
            SetPixels(sourcePixels, yOffset, 0, sourcePixels.Length);
        }

        /// <summary>
        /// Sets scanline pixels to the buffer.
        /// </summary>
        /// <typeparam name="T">Type of the pixel data</typeparam>
        /// <param name="sourcePixels">Source pixel buffer</param>
        /// <param name="yOffset">The y line offset.</param>
        /// <param name="pixelIndex">Offset into the source <paramref name="sourcePixels"/> buffer.</param>
        /// <param name="pixelCount">Number of pixels to write into the source <paramref name="sourcePixels"/> buffer.</param>
        /// <exception cref="System.ArgumentException">If the sizeof(T) is an invalid size</exception>
        /// <remarks>
        /// This method is working on a row basis. The <paramref name="yOffset"/> is specifying the first row to get 
        /// the pixels from.
        /// </remarks>
        public unsafe void SetPixels<T>(T[] sourcePixels, int yOffset, int pixelIndex, int pixelCount) where T : struct
        {
            var pixelPointer = (byte*)DataPointer + yOffset * rowStride;
            if (isStrictRowStride)
            {
                Utilities.Write(new IntPtr(pixelPointer), sourcePixels, 0, pixelCount);
            }
            else
            {
                var sizeOfOutputPixel = Utilities.SizeOf<T>() * pixelCount;
                var sizePerWidth = sizeOfOutputPixel / Width;
                var remainingPixels = sizeOfOutputPixel % Width;
                for (int i = 0; i < sizePerWidth; i++)
                {
                    Utilities.Write(new IntPtr(pixelPointer), sourcePixels, pixelIndex, Width);
                    pixelPointer += rowStride;
                    pixelIndex += Width;
                }
                if (remainingPixels > 0)
                {
                    Utilities.Write(new IntPtr(pixelPointer), sourcePixels, pixelIndex, remainingPixels);
                }
            }
        }

        public byte[][,] GetComponents()
        {
            return GetComponentArrayFromBuffer();
        }

        public ComponentsBuffer ToComponentsBuffer()
        {
            var raster = GetComponentArrayFromBuffer();
            var colorModel = new ColorModel() { Colorspace = ColorSpace.RGB, Opaque = true };
            return new ComponentsBuffer(colorModel, raster);
        }

        private byte[][,] GetComponentArrayFromBuffer()
        {
            var componentsArray = new byte[pixelSize][,];
            if (pixelSize == 1)
            {
                var colors = GetPixels<byte>();
                var redChannel = new byte[Width, Height];
                for (int i = 0; i < Height; ++i)
                {
                    for (int k = 0; k < Width; ++k)
                    {
                        redChannel[i, k] = colors[i * Width + k];
                    }
                }
                componentsArray[0] = redChannel;
            }
            else if (pixelSize == 2)
            {
                var colors = GetPixels<ColorRG>();
                var redChannel = new byte[Width, Height];
                var greenChannel = new byte[Width, Height];
                for (int i = 0; i < Height; ++i)
                {
                    for (int k = 0; k < Width; ++k)
                    {
                        redChannel[i, k] = colors[i * Width + k].R;
                        greenChannel[i, k] = colors[i * Width + k].G;
                    }
                }
                componentsArray[0] = redChannel;
                componentsArray[1] = greenChannel;
            }
            else if (pixelSize == 3)
            {
                var colors = GetPixels<ColorRGB>();
                var redChannel = new byte[Width, Height];
                var greenChannel = new byte[Width, Height];
                var blueChannel = new byte[Width, Height];
                for (int i = 0; i < Width ; ++i)
                {
                    for (int k = 0; k < Height; ++k)
                    {
                        redChannel[i, k] = colors[i * Width + k].R;
                        greenChannel[i, k] = colors[i * Width + k].G;
                        blueChannel[i, k] = colors[i * Width + k].G;
                    }
                }
                componentsArray[0] = redChannel;
                componentsArray[1] = greenChannel;
                componentsArray[2] = blueChannel;
            }
            else if (pixelSize == 4)
            {
                var colors = GetPixels<ColorRGBA>();
                var redChannel = new byte[Width, Height];
                var greenChannel = new byte[Width, Height];
                var blueChannel = new byte[Width, Height];
                var alphaChannel = new byte[Width, Height];
                for (int i = 0; i < Height; ++i)
                {
                    for (int k = 0; k < Width; ++k)
                    {
                        redChannel[i, k] = colors[i * Width + k].R;
                        greenChannel[i, k] = colors[i * Width + k].G;
                        blueChannel[i, k] = colors[i * Width + k].G;
                        alphaChannel[i, k] = colors[i * Width + k].A;
                    }
                }
                componentsArray[0] = redChannel;
                componentsArray[1] = greenChannel;
                componentsArray[2] = blueChannel;
                componentsArray[3] = alphaChannel;
            }
            return componentsArray;
        }

        public void FlipBuffer(FlipBufferOptions flipOtions)
        {
            var buffer = new byte[bufferStride];
            var flipped = new byte[bufferStride];
            Utilities.Read(DataPointer, buffer, 0, bufferStride);
            if (flipOtions == FlipBufferOptions.FlipVertically)
            {
                int offset = 0;
                for (int i = Height - 1; i >= 0; --i)
                {
                    System.Buffer.BlockCopy(buffer, i * rowStride, flipped, offset, rowStride);
                    offset += rowStride;
                }
            }
            else if (flipOtions == FlipBufferOptions.FlipHorizontally)
            {
                int offset;
                for (int i = 0; i < Height; ++i)
                {
                    var originalOffset = i * rowStride;
                    offset = ((i + 1) * rowStride) - pixelSize;
                    for (int k = 0; k < Width; ++k)
                    {
                        System.Buffer.BlockCopy(buffer, originalOffset, flipped, offset, pixelSize);
                        offset -= pixelSize;
                        originalOffset += pixelSize;
                    }
                }
            }
            else
            {
                int rowOffset = 0;
                for (int i = Height - 1; i >= 0; --i)
                {
                    System.Buffer.BlockCopy(buffer, i * rowStride, flipped, rowOffset, rowStride);
                    var originalOffset = i * rowStride;
                    var columnOffset = rowOffset + rowStride - pixelSize;
                    for (int k = 0; k < Width; ++k)
                    {
                        System.Buffer.BlockCopy(buffer, originalOffset, flipped, columnOffset, pixelSize);
                        columnOffset -= pixelSize;
                        originalOffset += pixelSize;
                    }
                    rowOffset += rowStride;
                }
            }
            Utilities.Write(DataPointer, flipped, 0, bufferStride);
        }
    }
}