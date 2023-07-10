using System;
using Adamantium.Imaging;

namespace Adamantium.UI.Media.Imaging;

public abstract class BitmapSource : ImageSource
{
   public BitmapSource()
   {
      DpiXScale = 1;
      DpiYScale = 1;
   }
   
   public virtual UInt32 PixelWidth => Texture.Width;

   public virtual UInt32 PixelHeight => Texture.Height;

   public virtual SurfaceFormat PixelFormat => Texture.SurfaceFormat;

   public override double Width => Texture.Width*DpiXScale;

   public override double Height => Texture.Height* DpiYScale;

   public Double DpiXScale { get; set; }

   public Double DpiYScale { get; set; }

   public void Save(Uri filePath, ImageFileType fileType)
   {
      Texture.Save(filePath.IsAbsoluteUri ? filePath.AbsolutePath : filePath.LocalPath, fileType);
   }
}