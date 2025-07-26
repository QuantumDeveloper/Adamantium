using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Media.Imaging;

[TypeParser(typeof(ImageSourceParser))]
public abstract class ImageSource : AdamantiumComponent, IDisposable
{
   public abstract double Width { get; }
   public abstract double Height { get; }
   
   public bool IsDisposed { get; private set; }

   protected virtual void ReleaseUnmanagedResources()
   {
      
   }
   
   private void Dispose(bool disposing)
   {
      if (!IsDisposed)
      {
         ReleaseUnmanagedResources();
         IsDisposed = true;
      }
   }

   public void Dispose()
   {
      Dispose(true);
      GC.SuppressFinalize(this);
   }

   ~ImageSource()
   {
      Dispose(false);
   }
}