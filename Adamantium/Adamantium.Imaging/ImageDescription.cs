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
using System.Runtime.InteropServices;
using AdamantiumVulkan.Core;
#pragma warning disable 1584,1581,1580

namespace Adamantium.Imaging
{
    /// <summary>
    /// A description for <see cref="Image"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ImageDescription : IEquatable<ImageDescription>
   {
        /// <summary>
        /// The dimension of a texture.
        /// </summary>
        public TextureDimension Dimension;

        /// <summary>	
        /// <p>Texture width (in texels).</p>
        /// </summary>	
        /// <remarks>
        /// This field is valid for all textures: <see cref="Texture"/>.
        /// </remarks>
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>unsigned int Width</unmanaged>	
        /// <unmanaged-short>unsigned int Width</unmanaged-short>	
        public uint Width;

        /// <summary>	
        /// <p>Texture height (in texels).</p>
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture2D"/>, <see cref="Texture3D"/> and <see cref="TextureCube"/>.
        /// </remarks>
        /// <msdn-id>ff476254</msdn-id>	
        /// <unmanaged>unsigned int Height</unmanaged>	
        /// <unmanaged-short>unsigned int Height</unmanaged-short>	
        public uint Height;

        /// <summary>	
        /// <p>Texture depth (in texels).</p>
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture3D"/>.
        /// </remarks>
        /// <msdn-id>ff476254</msdn-id>	
        /// <unmanaged>unsigned int Depth</unmanaged>	
        /// <unmanaged-short>unsigned int Depth</unmanaged-short>	
        public uint Depth;

        /// <summary>	
        /// <p>Number of textures in the array</p>
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture1D"/>, <see cref="Texture2D"/> and <see cref="TextureCube"/>
        /// </remarks>
        /// <remarks>
        /// </remarks>
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>unsigned int ArraySize</unmanaged>	
        /// <unmanaged-short>unsigned int ArraySize</unmanaged-short>	
        public uint ArraySize;

        /// <summary>	
        /// <p>The maximum number of mipmap levels in the texture.</p>
        /// </summary>	
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>unsigned int MipLevels</unmanaged>	
        /// <unmanaged-short>unsigned int MipLevels</unmanaged-short>	
        public uint MipLevels;

        /// <summary>	
        /// <dd> <p>Texture format (see <strong><see cref="AdamantiumVulkan.Core.Format"/></strong>).</p> </dd>	
        /// </summary>	
        /// <unmanaged>Vulkan imange Format</unmanaged>	
        public Format Format;

        public long RowStride => Width * Format.SizeOfInBytes();

        public long TotalSizeInBytes => RowStride * Height;

        /// <inheritdoc />
        public bool Equals(ImageDescription other)
        {
            return Dimension.Equals(other.Dimension) && Width == other.Width && Height == other.Height && Depth == other.Depth && ArraySize == other.ArraySize && MipLevels == other.MipLevels && Format.Equals(other.Format);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ImageDescription && Equals((ImageDescription) obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Dimension.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Width;
                hashCode = (hashCode * 397) ^ (int)Height;
                hashCode = (hashCode * 397) ^ (int)Depth;
                hashCode = (hashCode * 397) ^ (int)ArraySize;
                hashCode = (hashCode * 397) ^ (int)MipLevels;
                hashCode = (hashCode * 397) ^ Format.GetHashCode();
                return hashCode;
            }
        }

        //void IDataSerializable.Serialize(BinarySerializer serializer)
        //{
        //    serializer.SerializeEnum(ref Dimension);
        //    serializer.Serialize(ref Width);
        //    serializer.Serialize(ref Height);
        //    serializer.Serialize(ref Depth);
        //    serializer.Serialize(ref ArraySize);
        //    serializer.Serialize(ref MipLevels);
        //    serializer.SerializeEnum(ref Format);
        //}

        /// <summary>
        /// Comparer operator
        /// </summary>
        /// <param name="left">The <see cref="ImageDescription"/></param>
        /// <param name="right">The <see cref="ImageDescription"/></param>
        /// <returns></returns>
        public static bool operator ==(ImageDescription left, ImageDescription right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Comparer operator
        /// </summary>
        /// <param name="left">The <see cref="ImageDescription"/></param>
        /// <param name="right">The <see cref="ImageDescription"/></param>
        /// <returns></returns>
        public static bool operator !=(ImageDescription left, ImageDescription right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return
               $"Dimension: {Dimension}, Width: {Width}, Height: {Height}, Depth: {Depth}, Format: {Format}, ArraySize: {ArraySize}, MipLevels: {MipLevels}";
        }

        public static ImageDescription Default2D(uint width, uint height, SurfaceFormat format)
        {
            var descr = new ImageDescription
            {
                Depth = 1,
                ArraySize = 1,
                Dimension = TextureDimension.Texture2D,
                MipLevels = 1,
                Width = width,
                Height = height,
                Format = format
            };

            return descr;
        }
    }
}