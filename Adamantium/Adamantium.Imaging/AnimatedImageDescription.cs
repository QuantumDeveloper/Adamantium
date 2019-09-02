using System;
using System.Runtime.InteropServices;

namespace Adamantium.Imaging
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
        public uint Width;

        /// <summary>	
        /// <dd> <p>Texture height (in texels).
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture2D"/>, <see cref="Texture3D"/> and <see cref="TextureCube"/>.
        /// </remarks>
        /// <msdn-id>ff476254</msdn-id>	
        /// <unmanaged>unsigned int Height</unmanaged>	
        /// <unmanaged-short>unsigned int Height</unmanaged-short>	
        public uint Height;

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

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) Width;
                hashCode = (hashCode * 397) ^ (int) Height;
                hashCode = (hashCode * 397) ^ (int) XOffset;
                hashCode = (hashCode * 397) ^ (int) YOffset;
                hashCode = (hashCode * 397) ^ DelayNumerator.GetHashCode();
                hashCode = (hashCode * 397) ^ DelayDenominator.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) SequenceNumber;
                hashCode = (hashCode * 397) ^ BytesPerPixel;
                return hashCode;
            }
        }

        public static bool operator ==(AnimatedImageDescription left, AnimatedImageDescription right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AnimatedImageDescription left, AnimatedImageDescription right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return
               $"Dimension: {Dimension}, Width: {Width}, Height: {Height}, Depth: {Depth}, PixelSize: {BytesPerPixel}, ArraySize: {ArraySize}, MipLevels: {MipLevels}";
        }
    }
}
