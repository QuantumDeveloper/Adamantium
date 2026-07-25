using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.UI.Core.Graphics;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Core.Media.Imaging;

public unsafe class BitmapSource : ImageSource
{
   public BitmapSource()
   {
      DpiXScale = 1;
      DpiYScale = 1;
      Layout = ImageLayout.ShaderReadOnlyOptimal;
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
      SurfaceLayout = format;
      _pixels = pixels;
      Depth = 1;
      Layout = ImageLayout.ShaderReadOnlyOptimal;
   }

   private byte[] _pixels;

   /// <summary>The CPU pixel buffer (may be null once uploaded to a GPU texture). For readback consumers - e.g. the
   /// drag ghost, which needs the raw bytes to hand the OS compositor.</summary>
   public byte[] PixelBytes => _pixels;

   public virtual ITexture Texture { get; protected set; }

   public virtual UInt32 PixelWidth { get; protected set; }

   public virtual UInt32 PixelHeight { get; protected set; }

   public virtual SurfaceFormat SurfaceLayout { get; protected set; }
   
   public int Depth { get; set; }

   public override double Width => PixelWidth * DpiXScale;

   public override double Height => PixelHeight * DpiYScale;

   public Double DpiXScale { get; set; }

   public Double DpiYScale { get; set; }
   
   public ImageLayout Layout { get; set; }
   
   public void* NativePointer => Texture == null ? null : Texture.NativePointer;

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

   public virtual ITexture GetOrCreateTexture(IResourceFactory factory)
   {
      if (IsDisposed)
      {
         throw new ObjectDisposedException(nameof(BitmapSource));
      }

      if (Texture == null)
      {
         // Async URI decode still in flight (or failed): no pixels yet. Hand back null - the caller skips drawing and
         // retries on a later re-render - instead of passing a null byte[] into texture creation (an NRE that killed
         // the render thread when a flip revealed a photo before its background decode completed).
         if (_pixels == null) return null;

         var textureDescription = new TextureDescription
         {
            Width = PixelWidth,
            Height = PixelHeight,
            Dimension = TextureDimension.Texture2D,
            Format = SurfaceLayout,
            Depth = 1,
            InitialLayout = ImageLayout.Undefined,
            ImageAspect = ImageAspectFlagBits.ColorBit,
            DesiredImageLayout = Layout,
            MipLevels = 1,
            ArrayLayers = 1
         };

         Texture = factory.CreateTexture(textureDescription, _pixels);
      }

      return Texture;
   }

   public event EventHandler<ExceptionEventArgs> DecodeFailed;
}