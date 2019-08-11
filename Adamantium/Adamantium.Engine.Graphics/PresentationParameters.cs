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
         PixelFormat = Format.R8G8B8A8_UNORM;
         DepthFormat = DepthFormat.Depth32;

         //RefreshRate = new Rational(60, 1);
         //BuffersCount = 2;
         //SwapEffect = SwapEffect.Discard;
         //Usage = Usage.RenderTargetOutput;
         //IsWindowed = true;
         //Scaling = Scaling.Stretch;
         //AlphaMode = AlphaMode.Unspecified;
      }

      public PresentationParameters(PresentationParameters parameters)
      {
         PresenterType = parameters.PresenterType;

         BackBufferWidth = parameters.BackBufferWidth;
         BackBufferHeight = parameters.BackBufferHeight;
         OutputHandle = parameters.OutputHandle;
         PixelFormat = parameters.PixelFormat;
         DepthFormat = parameters.DepthFormat;
         MSAALevel = parameters.MSAALevel;

         RefreshRate = parameters.RefreshRate;
         BuffersCount = parameters.BuffersCount;
         IsWindowed = parameters.IsWindowed;
         Flags = parameters.Flags;
         Scaling = parameters.Scaling;
         Usage = parameters.Usage;
         AlphaMode = parameters.AlphaMode;
      }

      public PresentationParameters(PresenterType presenterType, Int32 backbufferWidth, Int32 backbufferHeight, IntPtr handle, MSAALevel msaaLevel = MSAALevel.None)
      {
         PresenterType = presenterType;

         BackBufferWidth = backbufferWidth;
         BackBufferHeight = backbufferHeight;
         OutputHandle = handle;

         MSAALevel = msaaLevel;
         PixelFormat = Format.R8G8B8A8_UNORM;
         DepthFormat = DepthFormat.Depth32Stencil8X24;

         //RefreshRate = new Rational(60, 1);
         //BuffersCount = 2;
         //SwapEffect = SwapEffect.Discard;
         //Usage = Usage.RenderTargetOutput;
         //IsWindowed = true;
         //Flags = SwapChainFlags.None;
         //Scaling = Scaling.Stretch;
         //AlphaMode = AlphaMode.Unspecified;
      }

      public PresentationParameters(PresenterType presenterType, Int32 backbufferWidth, Int32 backbufferHeight, IntPtr handle, MSAALevel msaa,
         Rational refreshRate, Format pixelFormat = Format.R8G8B8A8_UNORM, DepthFormat depthFormat = DepthFormat.Depth32Stencil8X24,
         Usage usage = Usage.RenderTargetOutput, SwapEffect swapEffect = SwapEffect.Discard, Int32 buffresCount = 2, Boolean isWindowed = true, 
         SwapChainFlags flags = SwapChainFlags.None, Scaling scaling = Scaling.Stretch, AlphaMode alphaMode = AlphaMode.Unspecified)
      {
         PresenterType = presenterType;

         BackBufferWidth = backbufferWidth;
         BackBufferHeight = backbufferHeight;
         OutputHandle = handle;
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

      public PresenterType PresenterType { get; }

      public Int32 BackBufferWidth { get; internal set; }

      public Int32 BackBufferHeight { get; internal set; }

      public IntPtr OutputHandle { get; }

      public SurfaceFormat PixelFormat
      {
         get; internal set;
      }

      public DepthFormat DepthFormat
      {
         get; internal set;
      }

      public MSAALevel MSAALevel { get; }

      public Boolean IsWindowed { get; internal set; }

      public Rational RefreshRate { get; }

      public Int32 BuffersCount
      {
         get; internal set;
      }

      public SwapEffect SwapEffect { get; }

      public Usage Usage
      {
         get;
      }

      public Scaling Scaling { get; }

      public AlphaMode AlphaMode { get; }

      public SwapChainFlags Flags { get; internal set; }

      public PresentationParameters Clone()
      {
         return (PresentationParameters)MemberwiseClone();
      }
   }

}
