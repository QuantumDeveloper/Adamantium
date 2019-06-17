using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// A description for animated <see cref="Image"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AnimatedImageDescription : IEquatable<AnimatedImageDescription>
    {
        /// <summary>
        /// The dimension of a texture.
        /// </summary>
        public TextureDimension Dimension => TextureDimension.Texture2D;

        /// <summary>	
        /// <dd> <p>Texture width (in texels).
        /// </summary>	
        /// <remarks>
        /// This field is valid for all textures: <see cref="Texture1D"/>, <see cref="Texture2D"/>, <see cref="Texture3D"/> and <see cref="TextureCube"/>.
        /// </remarks>
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>unsigned int Width</unmanaged>	
        /// <unmanaged-short>unsigned int Width</unmanaged-short>	
        public int Width;

        /// <summary>	
        /// <dd> <p>Texture height (in texels).
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture2D"/>, <see cref="Texture3D"/> and <see cref="TextureCube"/>.
        /// </remarks>
        /// <msdn-id>ff476254</msdn-id>	
        /// <unmanaged>unsigned int Height</unmanaged>	
        /// <unmanaged-short>unsigned int Height</unmanaged-short>	
        public int Height;

        /// <summary>
        /// Horizontal offset for current frame from the top left corner
        /// </summary>
        public uint XOffset;

        /// <summary>
        /// Vertical offset for current frame from the top left corner
        /// </summary>
        public uint YOffset;

        /// <summary>
        /// Frame delay fraction numerator
        /// </summary>
        public ushort DelayNumerator;

        /// <summary>
        /// Frame delay fraction denominator
        /// </summary>
        public ushort DelayDenominator;

        /// <summary>
        /// Number of cirrent frame
        /// </summary>
        public uint SequenceNumber;

        /// <summary>	
        /// <dd> <p>Texture depth (in texels).
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture3D"/>.
        /// </remarks>
        /// <msdn-id>ff476254</msdn-id>	
        /// <unmanaged>unsigned int Depth</unmanaged>	
        /// <unmanaged-short>unsigned int Depth</unmanaged-short>	
        public int Depth => 1;

        /// <summary>	
        /// <dd> <p>Number of textures in the array
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture1D"/>, <see cref="Texture2D"/> and <see cref="TextureCube"/>
        /// </remarks>
        /// <remarks>
        /// This field is only valid for textures: <see cref="Texture1D"/>, <see cref="Texture2D"/> and <see cref="TextureCube"/>.
        /// </remarks>
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>unsigned int ArraySize</unmanaged>	
        /// <unmanaged-short>unsigned int ArraySize</unmanaged-short>	
        public int ArraySize => 1;

        /// <summary>	
        /// <dd> <p>The maximum number of mipmap levels in the texture.
        /// </summary>	
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>unsigned int MipLevels</unmanaged>	
        /// <unmanaged-short>unsigned int MipLevels</unmanaged-short>	
        public int MipLevels => 1;

        /// <summary>	
        /// <dd> <p>Texture format (see <strong><see cref="AdamantiumVulkan.Core.Format"/></strong>).</p> </dd>	
        /// </summary>	
        /// <unmanaged>Vulkan imange Format</unmanaged>	
        public int BytesPerPixel;

        public bool Equals(AnimatedImageDescription other)
        {
            return Dimension.Equals(other.Dimension) && Width == other.Width && Height == other.Height && Depth == other.Depth && ArraySize == other.ArraySize && MipLevels == other.MipLevels && BytesPerPixel == other.BytesPerPixel;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is AnimatedImageDescription && Equals((AnimatedImageDescription)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Dimension.GetHashCode();
                hashCode = (hashCode * 397) ^ Width;
                hashCode = (hashCode * 397) ^ Height;
                hashCode = (hashCode * 397) ^ Depth;
                hashCode = (hashCode * 397) ^ ArraySize;
                hashCode = (hashCode * 397) ^ MipLevels;
                hashCode = (hashCode * 397) ^ BytesPerPixel;
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

        public static bool operator ==(AnimatedImageDescription left, AnimatedImageDescription right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AnimatedImageDescription left, AnimatedImageDescription right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return
               $"Dimension: {Dimension}, Width: {Width}, Height: {Height}, Depth: {Depth}, PixelSize: {BytesPerPixel}, ArraySize: {ArraySize}, MipLevels: {MipLevels}";
        }
    }
}
