using System;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Media.Imaging;

public abstract class ImageSource : AdamantiumComponent, IDisposable
{
   public virtual Texture Texture { get; set; }

   public abstract double Width { get; }
   public abstract double Height { get; }
   
   public bool IsDisposed { get; private set; }

   public void Dispose()
   {
      if (!IsDisposed)
      {
         Texture?.Dispose();
         Texture = null;
         IsDisposed = true;
      }
   }
}