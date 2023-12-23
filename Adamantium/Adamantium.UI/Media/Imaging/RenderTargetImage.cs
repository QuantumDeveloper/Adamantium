using System;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Media.Imaging;

public sealed unsafe class RenderTargetImage : BitmapSource
{
   public RenderTargetImage(DrawingContext drawingContext, 
      UInt32 width, 
       UInt32 height, 
       MSAALevel msaa, 
       SurfaceFormat format, 
       ImageLayout desiredLayout = ImageLayout.ShaderReadOnlyOptimal)
   {
       CreateTexture(drawingContext, width, height, msaa, format, desiredLayout);
    }

    private void CreateTexture(DrawingContext drawingContext,
       UInt32 width, 
       UInt32 height, 
       MSAALevel msaa, 
       SurfaceFormat format, 
       ImageLayout desiredLayout)
    {
       Texture = RenderTarget.New(drawingContext.GraphicsDevice, width, height, msaa, format, ImageUsageFlagBits.TransferDstBit, desiredLayout);
    }

    public void* NativePointer => Texture == null ? null : Texture.NativePointer;
}