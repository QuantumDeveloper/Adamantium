﻿using System;
using Adamantium.Imaging;

namespace Adamantium.UI.Media.Imaging
{
   public abstract class BitmapSource:ImageSource
   {
      public virtual Int32 PixelWidth => DXTexture.Width;

      public virtual Int32 PixelHeight => DXTexture.Height;

      public virtual SurfaceFormat PixelFormat => DXTexture.Format;

      public override double Width => DXTexture.Width*DpiXScale;

      public override double Height => DXTexture.Height* DpiYScale;

      public Double DpiXScale { get; set; }

      public Double DpiYScale { get; set; }

      public void Save(Uri filePath, ImageFileType fileType)
      {
         DXTexture.Save(filePath.IsAbsoluteUri ? filePath.AbsolutePath : filePath.LocalPath, fileType);
      }
   }
}
