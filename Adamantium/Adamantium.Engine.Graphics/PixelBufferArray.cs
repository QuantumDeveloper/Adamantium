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

using System;
using System.Collections;
using System.Collections.Generic;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Used by <see cref="Image"/> to provide a selector to a <see cref="PixelBuffer"/>.
    /// </summary>
    public sealed class PixelBufferArray : IEnumerable<PixelBuffer>
    {
        private readonly Image image;

        internal PixelBufferArray(Image image)
        {
            this.image = image;
        }

        /// <summary>
        /// Gets the pixel buffer.
        /// </summary>
        /// <returns>A <see cref="PixelBuffer"/>.</returns>
        public PixelBuffer this[int bufferIndex]
        {
            get
            {
                return this.image.pixelBuffers[bufferIndex];
            }
        }

        /// <summary>
        /// Gets the total number of pixel buffers.
        /// </summary>
        /// <returns>The total number of pixel buffers.</returns>
        public int Count { get { return this.image.pixelBuffers.Length; } }

        public object Current => throw new NotImplementedException();

        /// <summary>
        /// Gets the pixel buffer for the specified array/z slice and mipmap level.
        /// </summary>
        /// <param name="arrayOrDepthSlice">For 3D image, the parameter is the Z slice, otherwise it is an index into the texture array.</param>
        /// <param name="mipIndex">The mip map slice index.</param>
        /// <returns>A <see cref="PixelBuffer"/>.</returns>
        public PixelBuffer this[int arrayOrDepthSlice, int mipIndex]
        {
            get
            {
                return this.image.GetPixelBuffer(arrayOrDepthSlice, mipIndex);
            }
        }

        /// <summary>
        /// Gets the pixel buffer for the specified array/z slice and mipmap level.
        /// </summary>
        /// <param name="arrayIndex">Index into the texture array. Must be set to 0 for 3D images.</param>
        /// <param name="zIndex">Z index for 3D image. Must be set to 0 for all 1D/2D images.</param>
        /// <param name="mipIndex">The mip map slice index.</param>
        /// <returns>A <see cref="PixelBuffer"/>.</returns>
        public PixelBuffer this[int arrayIndex, int zIndex, int mipIndex]
        {
            get
            {
                return this.image.GetPixelBuffer(arrayIndex, zIndex, mipIndex);
            }
        }

        public IEnumerator GetEnumerator()
        {
            return image.pixelBuffers.GetEnumerator();
        }

        IEnumerator<PixelBuffer> IEnumerable<PixelBuffer>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public struct Enumerator : IEnumerator<PixelBuffer>
        {
            private readonly PixelBufferArray collection;

            private int index;

            private PixelBuffer current;

            internal Enumerator(PixelBufferArray collection)
            {
                this.collection = collection;
                index = 0;
                current = default(PixelBuffer);
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (index < collection.Count)
                {
                    current = collection[index];
                    index++;
                    return true;
                }
                else
                {
                    index = 0;
                    current = default(PixelBuffer);
                    return false;
                }
            }

            public void Reset()
            {
                index = 0;
                current = default(PixelBuffer);
            }

            object IEnumerator.Current => current;

            public PixelBuffer Current => current;
        }


        public static implicit operator PixelBuffer[] (PixelBufferArray pixelBuffer)
        {
            return pixelBuffer.image.pixelBuffers;
        }
    }
}