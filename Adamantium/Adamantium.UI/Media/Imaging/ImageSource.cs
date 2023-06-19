using System;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Media.Imaging;

public abstract class ImageSource : AdamantiumComponent, IDisposable
{
   public virtual Texture Texture { get; set; }

   public abstract double Width { get; }
   public abstract double Height { get; }

   public void Dispose()
   {
      Texture?.Dispose();
      Texture = null;
   }
}