using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.UI.Core.Graphics;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Core.Media.Imaging;

public sealed class RenderTargetImage : BitmapSource
{
   public RenderTargetImage(
      UInt32 width,
      UInt32 height,
      MSAALevel msaa,
      SurfaceFormat surfaceLayout,
      ImageLayout desiredLayout = ImageLayout.ShaderReadOnlyOptimal)
   {
      PixelWidth = width;
      PixelHeight = height;
      SurfaceLayout = surfaceLayout;
      MsaaLevel = msaa;
      Layout = desiredLayout;
   }
   
   public MSAALevel MsaaLevel { get; set; }

   public override ITexture GetOrCreateTexture(IResourceFactory factory)
   {
      if (IsDisposed)
      {
         throw new ObjectDisposedException(nameof(BitmapSource));
      }

      return Texture ??= factory.CreateRenderTarget((uint)Width, (uint)Height, MsaaLevel, SurfaceLayout, Layout);
   }
    
}