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
      
      // Read back, not presented: one frame is rendered and waited on, so a ring would only blur which copy holds the
      // result the caller is about to save.
      protected override int FrameCopies => 1;

      private void CreateRenderTarget()
      {
         renderTargets = new IRenderTarget[1];
         renderTargets[0] = ToDispose(GraphicsDevice.CreateRenderTarget(Width, Height, MSAALevel, SurfaceFormat));
      }

      public ITexture ResolveTexture => renderTargets[0].ResolveTexture;

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

         // Resize recreates images the previous frame may still read; freeing them in flight loses the device. Resize is
         // rare, so a full wait-idle is enough.
         GraphicsDevice.DeviceWaitIdle();

         DisposeFrameSurfaces();


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
