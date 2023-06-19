using System;
using System.Net.Mime;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.Imaging.Dds;
using AdamantiumVulkan.Core;

//using Texture2D = Adamantium.Engine.Graphics.Texture2D;

namespace Adamantium.UI.Media.Imaging;

public sealed unsafe class RenderTargetImage : BitmapSource
{
   public RenderTargetImage(UInt32 width, 
       UInt32 height, 
       MSAALevel msaa, 
       SurfaceFormat format, 
       ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal)
    {
       CreateTexture(width, height, msaa, format, desiredLayout);
    }

    private void CreateTexture(UInt32 width, 
       UInt32 height, 
       MSAALevel msaa, 
       SurfaceFormat format, 
       ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal)
    {
       var deviceService = UIApplication.Current.Container.Resolve<IGraphicsDeviceService>();
       try
       {
          Texture = RenderTarget.New(deviceService.ResourceLoaderDevice, width, height, msaa, format, desiredLayout);
       }
       catch (Exception exception)
       {
          RenderTargetCreationFailed?.Invoke(this, new ExceptionEventArgs(exception));
       }
    }

    public void* NativePointer => Texture.NativePointer;

    public event EventHandler<ExceptionEventArgs> RenderTargetCreationFailed;
}