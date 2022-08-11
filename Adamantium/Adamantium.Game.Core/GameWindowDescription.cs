using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.Game.Core
{
    public class GameWindowDescription : PropertyChangedBase
    {
        public GameWindowDescription(PresenterType presenterType)
        {
            PresenterType = presenterType;

            MsaaLevel = MSAALevel.None;
            PixelFormat = Format.B8G8R8A8_SNORM;
            DepthFormat = DepthFormat.Depth32Stencil8X24;
            BuffersCount = 3;
        }

        public GameWindowDescription(PresentationParameters parameters)
        {
            PresenterType = parameters.PresenterType;

            Width = parameters.Width;
            Height = parameters.Height;
            Handle = parameters.OutputHandle;
            PixelFormat = parameters.ImageFormat;
            DepthFormat = parameters.DepthFormat;
            MsaaLevel = parameters.MSAALevel;

            BuffersCount = parameters.BuffersCount;
        }

        public GameWindowDescription(
            PresenterType presenterType, 
            UInt32 width, 
            UInt32 height, 
            IntPtr handle)
        {
            PresenterType = presenterType;

            Width = width;
            Height = height;
            Handle = handle;

            MsaaLevel = MSAALevel.None;
            PixelFormat = Format.R8G8B8A8_UNORM;
            DepthFormat = DepthFormat.Depth32Stencil8X24;
            
            BuffersCount = 3;
        }

        public GameWindowDescription(
            PresenterType presenterType, 
            UInt32 width, 
            UInt32 height, 
            IntPtr handle, 
            MSAALevel msaa,
            Format pixelFormat = Format.R8G8B8A8_UNORM, 
            DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24, 
            UInt32 buffersCount = 3)
        {
           PresenterType = presenterType;

           Width = width;
           Height = height;
           Handle = handle;
           PixelFormat = pixelFormat;
           DepthFormat = depthFormat;
           MsaaLevel = msaa;
           BuffersCount = buffersCount;
        }

        public UInt32 Width { get; set; }
        public UInt32 Height { get; set; }
        public IntPtr Handle { get; set; }
        public SurfaceFormat PixelFormat { get; set; }
        public DepthFormat DepthFormat { get; set; }
        public MSAALevel MsaaLevel { get; set; }
        public UInt32 BuffersCount { get; set; }

        public PresenterType PresenterType { get; }

        public PresentInterval PresentInterval { get; set; }

        public GameWindowDescription Clone()
        {
            return (GameWindowDescription)MemberwiseClone();
        }

        public PresentationParameters ToPresentationParameters()
        {
            return new PresentationParameters(
                PresenterType, 
                Width, 
                Height, 
                Handle, 
                MsaaLevel, 
                PixelFormat, 
                DepthFormat, 
                BuffersCount);
        }
        
        public static implicit operator PresentationParameters(GameWindowDescription description)
        {
            return description.ToPresentationParameters();
        }
    }
}
