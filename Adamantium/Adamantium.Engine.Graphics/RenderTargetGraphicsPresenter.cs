﻿using Adamantium.Imaging;
using System;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
   public class RenderTargetGraphicsPresenter:GraphicsPresenter
   {
      //private SharpDX.Direct3D11.Texture2D baseTexture;
      //private SharpDX.Direct3D11.Texture2D sharedTexture;
      private bool isShared = false;

      public RenderTargetGraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description,
         string name = "") : base(graphicsDevice, description, name)
      {
         CreateBackBuffer();
      }

      private void CreateBackBuffer()
      {
         //RemoveAndDispose(ref backbuffer);
         //RemoveAndDispose(ref baseTexture);
         //RemoveAndDispose(ref sharedTexture);

         //baseTexture = ToDispose(new SharpDX.Direct3D11.Texture2D(Description.OutputHandle));
         //isShared = baseTexture.Description.OptionFlags.HasFlag(ResourceOptionFlags.Shared);

         //if (isShared)
         //{
         //   sharedTexture = ToDispose(GraphicsDevice.GetSharedResource<SharpDX.Direct3D11.Texture2D>(baseTexture));
         //   baseTexture.Dispose();
         //}

         //TextureDescription textureDesc = new TextureDescription()
         //{
         //   Format = Description.PixelFormat,
         //   ArraySize = 1,
         //   MipLevels = 1,
         //   Width = Description.BackBufferWidth,
         //   Height = Description.BackBufferHeight,
         //   SampleDescription = new SampleDescription((Int32)Description.MSAALevel, 0),
         //   Usage = ResourceUsage.Default,
         //   BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
         //   CpuAccessFlags = CpuAccessFlags.None,
         //   OptionFlags = ResourceOptionFlags.None
         //};

         //// Renderview on the backbuffer
         //backbuffer = ToDispose(RenderTarget2D.New(GraphicsDevice, textureDesc));
      }

      /// <summary>
      /// Resize graphics presenter bacbuffer according to width and height
      /// </summary>
      /// <param name="parameters"></param>
      public override bool Resize(PresentationParameters parameters)
      {
         if (!base.Resize(parameters))
         {
            return false;
         }

         //RemoveAndDispose(ref baseTexture);
         //RemoveAndDispose(ref sharedTexture);

         //baseTexture = ToDispose(new SharpDX.Direct3D11.Texture2D(Description.OutputHandle));
         //isShared = baseTexture.Description.OptionFlags.HasFlag(ResourceOptionFlags.Shared);

         //if (isShared)
         //{
         //   sharedTexture = ToDispose(GraphicsDevice.GetSharedResource<SharpDX.Direct3D11.Texture2D>(baseTexture));
         //   baseTexture.Dispose();
         //}

         CreateBackBuffer();

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
