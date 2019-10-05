using System.Runtime.InteropServices;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Decribes texture parameters which will be used to create or load texture
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TextureDescription
    {
        /// <summary>
        /// The dimension of a texture.
        /// </summary>
        public TextureDimension Dimension;

        /// <summary>	
        /// <dd> <p>Texture width (in texels). The  range is from 1 to <see cref="SharpDX.Direct3D11.Resource.MaximumTexture1DSize"/> (16384). However, the range is actually constrained by the feature level at which you create the rendering device. For more information about restrictions, see Remarks.</p> </dd>	
        /// </summary>	
        /// <remarks>
        /// This field is valid for all textures: <see cref="Texture1D"/>, <see cref="Texture2D"/>, <see cref="Texture3D"/> and <see cref="TextureCube"/>.
        /// </remarks>
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>unsigned int Width</unmanaged>	
        /// <unmanaged-short>unsigned int Width</unmanaged-short>	
        public uint Width;

        /// <summary>	
        /// <dd> <p>Texture height (in texels). The  range is from 1 to <see cref="SharpDX.Direct3D11.Resource.MaximumTexture3DSize"/> (2048). However, the range is actually constrained by the feature level at which you create the rendering device. For more information about restrictions, see Remarks.</p> </dd>	
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture2D"/>, <see cref="Texture3D"/> and <see cref="TextureCube"/>.
        /// </remarks>
        /// <msdn-id>ff476254</msdn-id>	
        /// <unmanaged>unsigned int Height</unmanaged>	
        /// <unmanaged-short>unsigned int Height</unmanaged-short>	
        public uint Height;

        /// <summary>	
        /// <dd> <p>Texture depth (in texels). The  range is from 1 to <see cref="SharpDX.Direct3D11.Resource.MaximumTexture3DSize"/> (2048). However, the range is actually constrained by the feature level at which you create the rendering device. For more information about restrictions, see Remarks.</p> </dd>	
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture3D"/>.
        /// </remarks>
        /// <msdn-id>ff476254</msdn-id>	
        /// <unmanaged>unsigned int Depth</unmanaged>	
        /// <unmanaged-short>unsigned int Depth</unmanaged-short>	
        public uint Depth;

        /// <summary>	
        /// <dd> <p>Number of textures in the array. The  range is from 1 to <see cref="SharpDX.Direct3D11.Resource.MaximumTexture1DArraySize"/> (2048). However, the range is actually constrained by the feature level at which you create the rendering device. For more information about restrictions, see Remarks.</p> </dd>	
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
        public uint ArrayLayers;

        /// <summary>	
        /// <dd> <p>The maximum number of mipmap levels in the texture. Use 1 for a multisampled texture; or 0 to generate a full set of subtextures.</p> </dd>	
        /// </summary>	
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>unsigned int MipLevels</unmanaged>	
        /// <unmanaged-short>unsigned int MipLevels</unmanaged-short>	
        public uint MipLevels;

        /// <summary>	
        /// <dd> <p>Texture format (see <strong><see cref="AdamantiumVulkan.Core.Format"/></strong>).</p> </dd>	
        /// </summary>	
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>DXGI_FORMAT Format</unmanaged>	
        /// <unmanaged-short>DXGI_FORMAT Format</unmanaged-short>	
        public Format Format;

        /// <summary>	
        /// <dd> <p>Structure that specifies multisampling parameters for the texture. See <strong><see cref="SampleCountFlagBits"/></strong>.</p> </dd>	
        /// </summary>	
        /// <remarks>
        /// This field is only valid for <see cref="Texture2D"/>.
        /// </remarks>
        /// <unmanaged-short>DXGI_SAMPLE_DESC SampleDesc</unmanaged-short>	
        public SampleCountFlagBits Samples;

        /// <summary>	
        /// <dd> <p>Value that identifies how the texture is to be read from and written to.</p> </dd>	
        /// </summary>	
        /// <unmanaged-short>ImageUsageFlagBits</unmanaged-short>	
        public ImageUsageFlagBits Usage;

        /// <summary>	
        /// <dd> <p>Flags (see <strong><see cref="SharingMode"/></strong>) for binding to pipeline stages. The flags can be combined by a logical OR. For a 1D texture, the allowable values are: <see cref="SharpDX.Direct3D11.BindFlags.ShaderResource"/>, <see cref="SharpDX.Direct3D11.BindFlags.RenderTarget"/> and <see cref="SharpDX.Direct3D11.BindFlags.DepthStencil"/>.</p> </dd>	
        /// </summary>	
        /// <msdn-id>ff476252</msdn-id>	
        /// <unmanaged>D3D11_BIND_FLAG BindFlags</unmanaged>	
        /// <unmanaged-short>D3D11_BIND_FLAG BindFlags</unmanaged-short>	
        public SharingMode SharingMode;

        public ImageTiling ImageTiling;

        public ImageCreateFlagBits Flags;

        public ImageType ImageType;

        public ImageLayout DesiredImageLayout;

        public ImageAspectFlagBits ImageAspect;

        /// <summary>
        /// Gets the staging description for this instance.
        /// </summary>
        /// <returns>A Staging description</returns>
        public TextureDescription ToStagingDescription()
        {
            var copy = this;
            copy.SharingMode = SharingMode.Exclusive;
            copy.Samples = SampleCountFlagBits._1Bit;
            copy.Usage = ImageUsageFlagBits.StorageBit;
            return copy;
        }

        public bool Equals(TextureDescription other)
        {
            return Dimension == other.Dimension &&
                Width == other.Width &&
                Height == other.Height &&
                Depth == other.Depth &&
                ArrayLayers == other.ArrayLayers &&
                MipLevels == other.MipLevels &&
                Format == other.Format &&
                Samples == other.Samples &&
                Usage == other.Usage &&
                SharingMode == other.SharingMode &&
                ImageTiling == other.ImageTiling &&
                ImageType == other.ImageType &&
                DesiredImageLayout == other.DesiredImageLayout &&
                Flags == other.Flags;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is TextureDescription && Equals((TextureDescription)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Dimension.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Width;
                hashCode = (hashCode * 397) ^ (int)Height;
                hashCode = (hashCode * 397) ^ (int)Depth;
                hashCode = (hashCode * 397) ^ (int)ArrayLayers;
                hashCode = (hashCode * 397) ^ (int)MipLevels;
                hashCode = (hashCode * 397) ^ Format.GetHashCode();
                hashCode = (hashCode * 397) ^ Samples.GetHashCode();
                hashCode = (hashCode * 397) ^ Usage.GetHashCode();
                hashCode = (hashCode * 397) ^ SharingMode.GetHashCode();
                hashCode = (hashCode * 397) ^ ImageTiling.GetHashCode();
                hashCode = (hashCode * 397) ^ ImageType.GetHashCode();
                hashCode = (hashCode * 397) ^ DesiredImageLayout.GetHashCode();
                hashCode = (hashCode * 397) ^ Flags.GetHashCode();

                return hashCode;
            }
        }

        public static bool operator ==(TextureDescription left, TextureDescription right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextureDescription left, TextureDescription right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Texture2DDescription"/> to <see cref="TextureDescription"/>.
        /// </summary>
        /// <param name="description">The texture description.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator TextureDescription(ImageCreateInfo description)
        {
            return new TextureDescription()
            {
                Dimension = (TextureDimension)description.ImageType,
                Width = description.Extent.Width,
                Height = description.Extent.Height,
                Depth = description.Extent.Depth,
                MipLevels = description.MipLevels,
                ArrayLayers = description.ArrayLayers,
                Format = description.Format,
                Samples = SampleCountFlagBits._1Bit,
                Usage = (ImageUsageFlagBits)description.Usage,
                SharingMode = description.SharingMode,
                ImageTiling = description.Tiling,
                ImageType = description.ImageType,
                DesiredImageLayout = description.InitialLayout,
                Flags = (ImageCreateFlagBits)description.Flags
            };
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="TextureDescription"/> to <see cref="Texture2DDescription"/>.
        /// </summary>
        /// <param name="description">The texture description.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator ImageCreateInfo(TextureDescription description)
        {
            return new ImageCreateInfo()
            {
                ImageType = description.ImageType,
                Extent = new Extent3D() { Width = description.Width, Height = description.Height, Depth = description.Depth },
                MipLevels = description.MipLevels,
                ArrayLayers = description.ArrayLayers,
                Format = description.Format,
                Samples = description.Samples,
                Usage = (uint)description.Usage,
                SharingMode = description.SharingMode,
                Tiling = description.ImageTiling,
                InitialLayout = description.DesiredImageLayout,
                Flags = (uint)description.Flags,
            };
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="ImageDescription"/> to <see cref="TextureDescription"/>.
        /// </summary>
        /// <param name="description">The image description.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator ImageDescription(TextureDescription description)
        {
            return new ImageDescription()
            {
                Dimension = description.Dimension,
                Width = description.Width,
                Height = description.Height,
                Depth = description.Depth,
                ArraySize = description.ArrayLayers,
                MipLevels = description.MipLevels,
                Format = description.Format,
            };
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="ImageDescription"/> to <see cref="TextureDescription"/>.
        /// </summary>
        /// <param name="description">The image description.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator TextureDescription(ImageDescription description)
        {
            return new TextureDescription()
            {
                Dimension = description.Dimension,
                Width = description.Width,
                Height = description.Height,
                Depth = description.Depth,
                ArrayLayers = description.ArraySize,
                MipLevels = description.MipLevels,
                Format = description.Format,
                Samples = SampleCountFlagBits._1Bit,
                Usage = ImageUsageFlagBits.SampledBit,
                ImageTiling = ImageTiling.Optimal,
                SharingMode = SharingMode.Exclusive,
            };
        }
    }
}
