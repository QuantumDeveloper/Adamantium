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
