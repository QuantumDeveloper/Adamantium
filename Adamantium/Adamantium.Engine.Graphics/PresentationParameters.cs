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
            ImageFormat = Format.B8G8R8A8_UNORM;
            DepthFormat = DepthFormat.Depth32Stencil8X24;
            BuffersCount = 3;
            ImageColorSpace = ColorSpace.SRGBNonlinear;
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
            BuffersCount = parameters.BuffersCount;

            MinImageCount = parameters.MinImageCount;
            ImageColorSpace = parameters.ImageColorSpace;

            // Should be 1 for monoscopic and 2 for stereoscopic swapchain
            ImageArrayLayers = parameters.ImageArrayLayers;
            ImageUsage = parameters.ImageUsage;
            ImageSharingMode = parameters.ImageSharingMode;
            PreTransform = parameters.PreTransform;
            PresentMode = parameters.PresentMode;
            Clipped = parameters.Clipped;
        }

        public PresentationParameters(
            PresenterType presenterType, 
            UInt32 width, 
            UInt32 height, 
            IntPtr handle, 
            MSAALevel msaaLevel = MSAALevel.None,
            Format imageFormat = Format.B8G8R8A8_UNORM,
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24,
            UInt32 buffersCount = 3)
        {
            PresenterType = presenterType;

            Width = width;
            Height = height;
            OutputHandle = handle;

            MSAALevel = msaaLevel;
            ImageFormat = imageFormat;
            DepthFormat = depthFormat;
            BuffersCount = buffersCount;
        }

        public PresenterType PresenterType { get; }
        public UInt32 Width { get; set; }
        public UInt32 Height { get; set; }
        public IntPtr OutputHandle { get; set; }
        public IntPtr HInstanceHandle { get; set; }
        public SurfaceFormat ImageFormat { get; set; }
        public DepthFormat DepthFormat { get; set; }
        public MSAALevel MSAALevel { get; set; }
        public UInt32 BuffersCount { get; set; }
        
        public SwapchainCreateFlags Flags { get; set; }
        
        public uint MinImageCount { get; set; }
        public ColorSpace ImageColorSpace { get; set; }

        // Should be 1 for monoscopic and 2 for stereoscopic swapchain
        public uint ImageArrayLayers { get; set; }
        public ImageUsage ImageUsage { get; set; }
        public SharingMode ImageSharingMode { get; set; }
        public SurfaceTransform PreTransform { get; set; }
        public PresentMode PresentMode { get; set; }
        public bool Clipped { get; set; }

        public PresentationParameters Clone()
        {
            return new PresentationParameters(this);
        }
    }
}
