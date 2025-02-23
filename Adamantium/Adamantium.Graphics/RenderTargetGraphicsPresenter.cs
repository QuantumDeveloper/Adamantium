using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using AdamantiumVulkan.Core;
using Image = AdamantiumVulkan.Core.Image;

namespace Adamantium.Graphics
{
   public class RenderTargetGraphicsPresenter : GraphicsPresenter
   {
      private ITexture _resolveTexture;

      public RenderTargetGraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description,
         string name = "") : base(graphicsDevice, description, name)
      {
         CreateRenderTarget();
      }
      
      private void CreateRenderTarget()
      {
         renderTarget = ToDispose(GraphicsDevice.CreateRenderTarget(Width, Height, MSAALevel, SurfaceFormat));
         _resolveTexture = ToDispose(GraphicsDevice.CreateRenderTarget(Width, Height, MSAALevel.None, SurfaceFormat));
      }

      public ITexture ResolveTexture => _resolveTexture;

      public override Image GetImage(uint index) => _resolveTexture.GetImage();

      public override ImageView GetImageView(uint index) => _resolveTexture.GetImageView();

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
         RemoveAndDispose(ref _resolveTexture);
         
         CreateDepthBuffer();
         CreateRenderTarget();

         return true;
      }

      /// <summary>
      /// Present rendered image on screen
      /// </summary>
      public override PresenterState Present()
      {
         return PresenterState.Success;
      }
   }
}
