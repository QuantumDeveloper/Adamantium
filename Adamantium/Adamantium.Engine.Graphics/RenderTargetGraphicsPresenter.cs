using Adamantium.Imaging;
using System;
using AdamantiumVulkan.Core;
using Image = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
   public class RenderTargetGraphicsPresenter : GraphicsPresenter
   {
      //private SharpDX.Direct3D11.Texture2D baseTexture;
      //private SharpDX.Direct3D11.Texture2D sharedTexture;
      private bool isShared = false;

      public RenderTargetGraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description,
         string name = "") : base(graphicsDevice, description, name)
      {
         CreateRenderTarget();
      }
      
      private void CreateRenderTarget()
      {
         renderTarget = ToDispose(RenderTarget.New(GraphicsDevice, Width, Height, MSAALevel, ImageFormat));
      }

      public override Image GetImage(uint index)
      {
         return renderTarget;
      }

      public override ImageView GetImageView(uint index)
      {
         return renderTarget;
      }

      /// <summary>
      /// Resize graphics presenter backbuffer according to width and height
      /// </summary>
      /// <param name="parameters"></param>
      public override bool Resize(PresentationParameters parameters)
      {
         if (!base.Resize(parameters))
         {
            return false;
         }
         
         RemoveAndDispose(ref renderTarget);
         CreateRenderTarget();

         return true;
      }

      /// <summary>
      /// Present rendered image on screen
      /// </summary>
      public override PresenterState Present()
      {
         
         
         return PresenterState.Success;
         //if (isShared)
         //{
         //   GraphicsDevice.ResolveSubresource(backbuffer, 0, sharedTexture, 0, Description.PixelFormat);
         //   GraphicsDevice.Flush();
         //}
         //else
         //{
         //   GraphicsDevice.ResolveSubresource(backbuffer, 0, baseTexture, 0, Description.PixelFormat);
         //}

         
      }
   }
}
