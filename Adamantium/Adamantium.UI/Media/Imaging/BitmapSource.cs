using System;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Media.Imaging;

public class BitmapSource : ImageSource
{
   public BitmapSource()
   {
      DpiXScale = 1;
      DpiYScale = 1;
   }
   
   public BitmapSource(
      UInt32 width, 
      UInt32 height, 
      double dpiX, 
      double dpiY, 
      SurfaceFormat format,
      byte[] pixels)
   {
      PixelWidth = width;
      PixelHeight = height;
      DpiXScale = dpiX;
      DpiYScale = dpiY;
      PixelFormat = format;
      _pixels = pixels;
   }

   private byte[] _pixels;
   
   public virtual Texture Texture { get; set; }

   public virtual UInt32 PixelWidth { get; protected set; }

   public virtual UInt32 PixelHeight { get; protected set; }

   public virtual SurfaceFormat PixelFormat { get; protected set; }
   
   public int Depth { get; set; }

   public override double Width => PixelWidth * DpiXScale;

   public override double Height => PixelHeight * DpiYScale;

   public Double DpiXScale { get; set; }

   public Double DpiYScale { get; set; }

   public void Save(Uri filePath, ImageFileType fileType)
   {
      Texture.Save(filePath.IsAbsoluteUri ? filePath.AbsolutePath : filePath.LocalPath, fileType);
   }
   
   protected override void ReleaseUnmanagedResources()
   {
      Texture?.Dispose();
      Texture = null;
   }

   protected void SetPixels(byte[] pixels)
   {
      _pixels = pixels;
   }

   internal void InitUnderlyingImage(DrawingContext context)
   {
      if (IsDisposed || Texture != null) return;
      
      var textureDescription = new TextureDescription();
      textureDescription.Width = PixelWidth;
      textureDescription.Height = PixelHeight;
      textureDescription.Dimension = TextureDimension.Texture2D;
      textureDescription.Format = PixelFormat;
      textureDescription.Depth = 1;
      textureDescription.InitialLayout = ImageLayout.Undefined;
      textureDescription.ImageAspect = ImageAspectFlagBits.ColorBit;
      textureDescription.DesiredImageLayout = ImageLayout.ShaderReadOnlyOptimal;
      textureDescription.MipLevels = 1;
      textureDescription.ArrayLayers = 1;
      
      Texture = Texture.CreateFrom(context.GraphicsDevice, textureDescription, _pixels);
   }

   public event EventHandler<ExceptionEventArgs> DecodeFailed;
}