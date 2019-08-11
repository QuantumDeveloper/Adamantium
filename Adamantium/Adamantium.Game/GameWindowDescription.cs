using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.Imaging.Dds;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine
{
   public class GameWindowDescription:PropertyChangedBase
   {
      public GameWindowDescription(PresenterType presenterType)
      {
         PresenterType = presenterType;

         MSAALevel = MSAALevel.None;
         PixelFormat = Format.R8G8B8A8_UNORM;
         DepthFormat = DepthFormat.Depth32Stencil8X24;

         RefreshRate = new Rational(60, 1);
         BuffersCount = 2;
         SwapEffect = SwapEffect.Discard;
         Usage = Usage.RenderTargetOutput;
         IsWindowed = true;
         Scaling = Scaling.Stretch;
         AlphaMode = AlphaMode.Unspecified;
      }

      public GameWindowDescription(PresentationParameters parameters)
      {
         PresenterType = parameters.PresenterType;

         Width = parameters.BackBufferWidth;
         Height = parameters.BackBufferHeight;
         Handle = parameters.OutputHandle;
         PixelFormat = parameters.PixelFormat;
         DepthFormat = parameters.DepthFormat;
         MSAALevel = parameters.MSAALevel;

         RefreshRate = parameters.RefreshRate;
         BuffersCount = parameters.BuffersCount;
         IsWindowed = parameters.IsWindowed;
         Flags = parameters.Flags;
         Usage = Usage.RenderTargetOutput;
      }

      public GameWindowDescription(PresenterType presenterType, Int32 width, Int32 height, IntPtr handle)
      {
         PresenterType = presenterType;

         Width = width;
         Height = height;
         Handle = handle;

         MSAALevel = MSAALevel.None;
         PixelFormat = Format.R8G8B8A8_UNORM;
         DepthFormat = DepthFormat.Depth32Stencil8X24;

         RefreshRate = new Rational(60, 1);
         BuffersCount = 2;
         SwapEffect = SwapEffect.Discard;
         Usage = Usage.RenderTargetOutput;
         IsWindowed = true;
         Flags = SwapChainFlags.None;
         Scaling = Scaling.Stretch;
         AlphaMode = AlphaMode.Unspecified;
      }

      public GameWindowDescription(PresenterType presenterType, Int32 width, Int32 height, IntPtr handle, MSAALevel msaa,
         Rational refreshRate,
         Format pixelFormat = Format.R8G8B8A8_UNORM, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24,
         Usage usage = Usage.RenderTargetOutput,
         SwapEffect swapEffect = SwapEffect.Discard, Int32 buffresCount = 2, Boolean isWindowed = true, SwapChainFlags flags = SwapChainFlags.None, 
         Scaling scaling = Scaling.Stretch, AlphaMode alphaMode = AlphaMode.Unspecified)
      {
         PresenterType = presenterType;

         Width = width;
         Height = height;
         Handle = handle;
         PixelFormat = pixelFormat;
         DepthFormat = depthFormat;
         MSAALevel = msaa;

         RefreshRate = refreshRate;
         BuffersCount = buffresCount;
         SwapEffect = swapEffect;
         Usage = usage;
         IsWindowed = isWindowed;
         Flags = flags;
         Scaling = scaling;
         AlphaMode = alphaMode;
      }

      public Int32 Width { get; set; }
      public Int32 Height { get; set; }
      public IntPtr Handle { get; set; }
      public SurfaceFormat PixelFormat { get; set; }
      public DepthFormat DepthFormat { get; set; }
      public MSAALevel MSAALevel { get; set; }

      public Rational RefreshRate { get; set; }
      public Int32 BuffersCount { get; set; }
      public Boolean IsWindowed { get; set; }
      public SwapEffect SwapEffect { get; set; }
      public Usage Usage { get; set; }
      public SwapChainFlags Flags { get; set; }
      public Scaling Scaling { get; set; }
      public AlphaMode AlphaMode { get; set; }

      public PresenterType PresenterType { get; }

      public PresentInterval PresentInterval { get; set; }

      public PresentFlags PresentFlags { get; set; }


      public GameWindowDescription Clone()
      {
         return (GameWindowDescription)MemberwiseClone();
      }

      public PresentationParameters ToPresentationParameters()
      {
         return new PresentationParameters(PresenterType, Width, Height, Handle, MSAALevel, RefreshRate, PixelFormat,
            DepthFormat, Usage, SwapEffect, BuffersCount, IsWindowed, Flags, Scaling, AlphaMode);
      }

      public Texture2DDescription ToTexture2DDescription()
      {
         Texture2DDescription rendertoTextureDescription = new Texture2DDescription();
         rendertoTextureDescription.Width = Width;
         rendertoTextureDescription.Height = Height;
         rendertoTextureDescription.MipLevels = 1;
         rendertoTextureDescription.ArraySize = 1;
         rendertoTextureDescription.Format = PixelFormat;
         rendertoTextureDescription.SampleDescription.Count = (Int32)MSAALevel;
         rendertoTextureDescription.SampleDescription.Quality = 0;
         rendertoTextureDescription.Usage = ResourceUsage.Default;
         rendertoTextureDescription.BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource;
         rendertoTextureDescription.OptionFlags = ResourceOptionFlags.None;
         rendertoTextureDescription.CpuAccessFlags = CpuAccessFlags.None;
         return rendertoTextureDescription;
      }

      public static implicit operator Texture2DDescription(GameWindowDescription description)
      {
         return description.ToTexture2DDescription();
      }

      public static implicit operator PresentationParameters(GameWindowDescription description)
      {
         return description.ToPresentationParameters();
      }
   }
}
