using Adamantium.Graphics.Core.Presentation;

namespace Adamantium.Graphics.Core
{
   public class RenderTargetGraphicsPresenter : GraphicsPresenter
   {
      public RenderTargetGraphicsPresenter(IGraphicsDevice graphicsDevice, PresentationParameters description,
         string name = "") : base(graphicsDevice, description, name)
      {
         CreateRenderTarget();
      }
      
      private void CreateRenderTarget()
      {
         renderTarget = ToDispose(GraphicsDevice.CreateRenderTarget(Width, Height, MSAALevel, SurfaceFormat));
      }

      public ITexture ResolveTexture => renderTarget.ResolveTexture;

      /// <summary>
      /// Resize graphics presenter backBuffer according to width and height
      /// </summary>
      /// <param name="parameters"></param>
      public override bool Resize(PresentationParameters parameters)
      {
         if (!base.Resize(parameters))
         {
            return false;
         }

         // Resize frees and recreates GPU images the previous frame may still be reading (this render target and
         // its resolve, plus the shared surface fed from the resolve). Idle the device first so nothing is in flight
         // when we destroy them - freeing a resource mid-flight loses the device, and the next allocation then throws
         // "failed to allocate image memory". Resize is rare, so a full wait-idle is the correct, simple guarantee
         // (same as SwapChainGraphicsPresenter).
         GraphicsDevice.DeviceWaitIdle();

         RemoveAndDispose(ref depthBuffer);
         RemoveAndDispose(ref renderTarget);
         
         CreateDepthBuffer();
         CreateRenderTarget();

         return true;
      }

      public override ITexture GetImageByIndex(uint index) => ResolveTexture;
      public override ITexture GetCurrentImage() => ResolveTexture;
      
      /// <summary>
      /// Present rendered image on screen
      /// </summary>
      public override PresenterState Present()
      {
         return PresenterState.Success;
      }
   }
}
