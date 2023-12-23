using System.Threading;
using AdamantiumVulkan.Core;
using Serilog;
using Image = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
   public class RenderTargetGraphicsPresenter : GraphicsPresenter
   {
      private Texture _resolveTexture;

      public RenderTargetGraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description,
         string name = "") : base(graphicsDevice, description, name)
      {
         CreateRenderTarget();
      }
      
      private void CreateRenderTarget()
      {
         renderTarget = ToDispose(RenderTarget.New(GraphicsDevice, Width, Height, MSAALevel, SurfaceFormat));
         _resolveTexture = ToDispose(RenderTarget.New(GraphicsDevice, Width, Height, MSAALevel.None, SurfaceFormat));
      }

      public Texture ResolveTexture => _resolveTexture;

      public override Image GetImage(uint index)
      {
         return _resolveTexture;
      }

      public override ImageView GetImageView(uint index)
      {
         return _resolveTexture;
      }

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
