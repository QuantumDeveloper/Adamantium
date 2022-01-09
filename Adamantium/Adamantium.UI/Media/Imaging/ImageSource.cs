using System;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Media.Imaging;

public abstract class ImageSource : AdamantiumComponent, IDisposable
{
   internal virtual Texture DXTexture { get; set; }

   public abstract double Width { get; }
   public abstract double Height { get; }

   public void Dispose()
   {
      DXTexture?.Dispose();
      DXTexture = null;
   }
}