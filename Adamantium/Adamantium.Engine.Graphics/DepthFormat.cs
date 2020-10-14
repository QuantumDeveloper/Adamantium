namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Defines the format of data in a depth-stencil buffer.
    /// </summary>
    public enum DepthFormat : uint
    {
        /// <summary>
        /// No depth stencil buffer.
        /// </summary>
        None = 0,

        /// <summary>
        /// A buffer that contains 16-bits of depth data.
        /// </summary>
        /// <msdn-id>bb173059</msdn-id>	
        /// <unmanaged>DXGI_FORMAT_D16_UNORM</unmanaged>	
        /// <unmanaged-short>DXGI_FORMAT_D16_UNORM</unmanaged-short>	
        Depth16 = AdamantiumVulkan.Core.Format.D16_UNORM,

        /// <summary>
        /// A buffer that contains 16-bits of depth data and 8 bits of stencil data.
        /// </summary>
        /// <msdn-id>bb173059</msdn-id>	
        /// <unmanaged>D16_UNORM_S8_UINT</unmanaged>	
        /// <unmanaged-short>D16_UNORM_S8_UINT</unmanaged-short>	
        Depth16Stencil8 = AdamantiumVulkan.Core.Format.D16_UNORM_S8_UINT,

        /// <summary>
        /// A 32 bit buffer that contains 24 bits of depth data and 8 bits of stencil data.
        /// </summary>
        /// <msdn-id>bb173059</msdn-id>	
        /// <unmanaged>DXGI_FORMAT_D24_UNORM_S8_UINT</unmanaged>	
        /// <unmanaged-short>DXGI_FORMAT_D24_UNORM_S8_UINT</unmanaged-short>	
        Depth24Stencil8 = AdamantiumVulkan.Core.Format.D24_UNORM_S8_UINT,

        /// <summary>
        /// A buffer that contains 32-bits of depth data.
        /// </summary>
        /// <msdn-id>bb173059</msdn-id>	
        /// <unmanaged>DXGI_FORMAT_D32_FLOAT</unmanaged>	
        /// <unmanaged-short>DXGI_FORMAT_D32_FLOAT</unmanaged-short>	
        Depth32 = AdamantiumVulkan.Core.Format.D32_SFLOAT,

        /// <summary>
        /// A double 32 bit buffer that contains 32 bits of depth data and 8 bits padded with 24 zero bits of stencil data.
        /// </summary>
        /// <msdn-id>bb173059</msdn-id>	
        /// <unmanaged>DXGI_FORMAT_D32_FLOAT_S8X24_UINT</unmanaged>	
        /// <unmanaged-short>DXGI_FORMAT_D32_FLOAT_S8X24_UINT</unmanaged-short>	
        Depth32Stencil8X24 = AdamantiumVulkan.Core.Format.D32_SFLOAT_S8_UINT,
    }
}
