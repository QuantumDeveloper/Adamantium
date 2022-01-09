using System;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
//using Texture2D = Adamantium.Engine.Graphics.Texture2D;

namespace Adamantium.UI.Media.Imaging;

public sealed class RenderTargetImage:BitmapSource
{
   //public RenderTargetImage(int width, int height, SurfaceFormat format, MSAALevel level, int arraysize = 1, 
   //   TextureFlags flags = TextureFlags.ShaderResource, ResourceUsage usage = ResourceUsage.Default, ResourceOptionFlags optionFlags = ResourceOptionFlags.None)
   //{
   //   CreateTexture(width, height, format, level, arraysize, flags, usage, optionFlags);
   //}

   //private void CreateTexture(int width, int height, SurfaceFormat format, MSAALevel level, int arraysize = 1,
   //   TextureFlags flags = TextureFlags.ShaderResource, ResourceUsage usage = ResourceUsage.Default, ResourceOptionFlags optionFlags = ResourceOptionFlags.None)
   //{
   //   var device = Application.Current.Services.Get<GraphicsDevice>();
   //   try
   //   {
   //      //ensure that this texture can be used as render target
   //      flags |= TextureFlags.RenderTarget;
   //      DXTexture = Texture2D.New(device, width, height, format, level, arraysize, flags, usage, optionFlags);
   //   }
   //   catch (Exception exception)
   //   {
   //      RendertargetCreationFailed?.Invoke(this, new ExceptionEventArgs(exception));
   //   }
   //}

   public IntPtr NativePointer => DXTexture.NativePointer;

   public event EventHandler<ExceptionEventArgs> RendertargetCreationFailed;
}