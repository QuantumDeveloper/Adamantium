using Adamantium.Imaging;
using AdamantiumVulkan.Core;
using System;

namespace Adamantium.Engine.Graphics
{
    public class PresentationParameters
    {
        public PresentationParameters(PresenterType presenterType)
        {
            PresenterType = presenterType;

            MSAALevel = MSAALevel.None;
            ImageFormat = Format.R8G8B8A8_UNORM;
            DepthFormat = DepthFormat.Depth32Stencil8X24;
            BuffersCount = 2;
            CompositeAlpha = AlphaMode.Opaque;
        }

        public PresentationParameters(PresentationParameters parameters)
        {
            PresenterType = parameters.PresenterType;

            Width = parameters.Width;
            Height = parameters.Height;
            OutputHandle = parameters.OutputHandle;
            ImageFormat = parameters.ImageFormat;
            DepthFormat = parameters.DepthFormat;
            MSAALevel = parameters.MSAALevel;
        }

        public PresentationParameters(PresenterType presenterType, UInt32 backbufferWidth, UInt32 backbufferHeight, IntPtr handle, MSAALevel msaaLevel = MSAALevel.None)
        {
            PresenterType = presenterType;

            Width = backbufferWidth;
            Height = backbufferHeight;
            OutputHandle = handle;

            MSAALevel = msaaLevel;
            ImageFormat = Format.R8G8B8A8_UNORM;
            DepthFormat = DepthFormat.Depth32Stencil8X24;
        }

        public PresenterType PresenterType { get; }
        public UInt32 Width { get; internal set; }
        public UInt32 Height { get; internal set; }
        public IntPtr OutputHandle { get; }
        public IntPtr HInstanceHandle { get; }
        public SurfaceFormat ImageFormat { get; internal set; }
        public DepthFormat DepthFormat { get; internal set; }
        public MSAALevel MSAALevel { get; }
        public UInt32 BuffersCount { get; internal set; }
        public SwapchainCreateFlags Flags { get; set; }
        public uint MinImageCount { get; set; }
        public ColorSpace ImageColorSpace { get; set; }

        //Should be 1 for monoscopic and 2 for stereoscopic swapchain
        public uint ImageArrayLayers { get; set; }
        public ImageUsage ImageUsage { get; set; }
        public SharingMode ImageSharingMode { get; set; }
        public SurfaceTransform PreTransform { get; set; }
        public AlphaMode CompositeAlpha { get; set; }
        public PresentMode PresentMode { get; set; }
        public bool Clipped { get; set; }

        public PresentationParameters Clone()
        {
            return (PresentationParameters)MemberwiseClone();
        }
    }
}
