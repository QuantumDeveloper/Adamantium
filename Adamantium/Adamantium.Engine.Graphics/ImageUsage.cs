using System;

namespace Adamantium.Engine.Graphics
{
    [Flags]
    public enum ImageUsage
    {
        TransferSrc = 1,

        TransferDst = 2,

        Sampled = 4,

        Storage = 8,

        ColorAttachment = 16,

        DepthStencilAttachment = 32,

        TransientAttachment = 64,

        InputAttachment = 128,

        ShadingRateImageNv = 256,

        FragmentDensityMapExt = 512,
    }

}
