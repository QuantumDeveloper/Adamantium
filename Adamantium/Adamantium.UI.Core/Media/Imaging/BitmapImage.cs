using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Core.Media.Imaging;

public sealed class BitmapImage : BitmapSource
{
   private IRawBitmap _rawBitmap;
   // Initialised HERE, not per-constructor: the IRawBitmap overload chains to base() rather than this(), so it used to
   // leave both caches null and every frame fetch threw (swallowed -> an animated source silently froze on frame 0).
   private readonly Queue<BitmapFrame> _framesCache = new();
   private readonly Dictionary<uint, BitmapFrame> _indexToFrame = new();

   public BitmapImage()
   {
   }

   public BitmapImage(Uri uri) : this()
   {
      UriSource = uri;
   }

   public BitmapImage(
      UInt32 width,
      UInt32 height,
      double dpiX,
      double dpiY,
      SurfaceFormat format,
      byte[] pixels) : base(
      width,
      height,
      dpiX,
      dpiY,
      format,
      pixels)
   {
   }

   public BitmapImage(IRawBitmap bitmap) : base(
      bitmap.Width,
      bitmap.Height,
      1,
      1,
      bitmap.PixelFormat,
      bitmap.GetRawPixels(0))
   {
      _rawBitmap = bitmap;
      // Built from a bitmap that is ALREADY decoded, so it is ready the moment it exists. Without saying so the
      // renderer skips it forever (it refuses to draw a BitmapImage that has not finished loading), which only the URI
      // path used to announce - and an image handed over as bytes never drew at all.
      IsLoaded = true;
   }

   public static readonly AdamantiumProperty UriSourceProperty = AdamantiumProperty.Register(nameof(UriSource),
      typeof(Uri), typeof(BitmapImage), new PropertyMetadata(null, UriChangedCallback));

   private static void UriChangedCallback(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
   {
      if (adamantiumAdamantiumComponent is BitmapImage bitmap)
      {
         var uri = (Uri)e.NewValue;
         if (uri.IsFile)
         {
            // Load OFF the UI thread: decoding (especially every frame of an animated APNG/GIF) is heavy and froze the UI
            // while a view full of images was being built (a multi-second tab-switch hang). Consumers await LoadTask
            // before reading frames (see Image.ProcessImageSource); FrameCount/GetFrame report empty until it completes.
            bitmap.LoadTask = Task.Run(() => bitmap.Load(uri));
         }
      }
   }
   
   public static readonly AdamantiumProperty FrameCacheLimitProperty = AdamantiumProperty.Register(nameof(FrameCacheLimit),
      typeof(uint), typeof(BitmapImage), new PropertyMetadata(10U, PropertyChangedCallback));

   private static void PropertyChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is BitmapImage bitmap)
      {
         // A lowered limit evicts the difference (subtract this way round: unsigned wraps).
         if (e.OldValue is uint oldLimit && e.NewValue is uint newLimit && newLimit < oldLimit)
         {
            bitmap.RemoveCacheItems(oldLimit - newLimit);
         }
      }
   }
   
   public static readonly AdamantiumProperty EnableFrameCacheProperty = AdamantiumProperty.Register(nameof(EnableFrameCache),
      typeof(bool), typeof(BitmapImage), new PropertyMetadata(true));

   public uint CurrentFrameIndex { get; private set; }

   public void ResetFrameIndex()
   {
      CurrentFrameIndex = StartFrame;
   }

   /// <summary>Pulls the playback cursor inside <see cref="StartFrame"/>..<see cref="EndFrame"/> - for when the range
   /// itself moves while the animation is running.</summary>
   public void ClampFrameIndex()
   {
      if (CurrentFrameIndex < StartFrame) CurrentFrameIndex = StartFrame;
      else if (CurrentFrameIndex > EndFrame) CurrentFrameIndex = EndFrame;
   }

   public bool EnableFrameCache
   {
      get => GetValue<bool>(EnableFrameCacheProperty);
      set => SetValue(EnableFrameCacheProperty, value);
   }

   public uint FrameCacheLimit
   {
      get => GetValue<uint>(FrameCacheLimitProperty);
      set => SetValue(FrameCacheLimitProperty, value);
   }

   public Uri UriSource
   {
      get => GetValue<Uri>(UriSourceProperty);
      set => SetValue(UriSourceProperty, value);
   }
   
   // Volatile: written on the background load thread AFTER the pixels/raw bitmap are filled, read on the render thread.
   // The release/acquire ordering guarantees that once a reader sees IsLoaded == true, those pixel writes are visible too
   // (so the OnRender "skip unloaded" guard never lets a half-populated bitmap reach texture creation).
   private volatile bool _isLoaded;
   public bool IsLoaded { get => _isLoaded; set => _isLoaded = value; }

   /// <summary>The background load kicked off when <see cref="UriSource"/> was set (null if the bitmap was built from raw
   /// data). Await it before reading frames so decoding never runs on the UI thread.</summary>
   public Task LoadTask { get; private set; }

   /// <summary>Completes when a URI-sourced bitmap has finished loading; completes immediately if there is no pending load.</summary>
   public Task EnsureLoadedAsync() => LoadTask ?? Task.CompletedTask;

   // Report empty until the (possibly still-running) background load has populated _rawBitmap, so an early read never NREs.
   public uint FrameCount => _rawBitmap?.FramesCount ?? 0;
   
   public uint StartFrame { get; set; }
   
   public uint EndFrame { get; set; }

   /// <summary>Whether an animation's frames are kept as LAYERS of one texture (the default) or as a texture per frame
   /// (the old path). Set ADAMANTIUM_FRAME_ARRAY=0 to fall back - kept while the array path is being proven out on this
   /// driver, so both can be compared from one build.</summary>
   public static bool UseFrameArrayTextures { get; set; } =
      Environment.GetEnvironmentVariable("ADAMANTIUM_FRAME_ARRAY") != "0";

   private volatile ITexture _frameArrayTexture;
   private int _frameArrayRequested;

   /// <summary>Every frame of this animation as the LAYERS of one texture: playing it is then choosing a layer - no
   /// upload, no allocation and no texture per frame (a 200-frame GIF used to mean 200 of them). Null until it is BUILT,
   /// which happens off the render thread - decoding every frame and uploading ~400 MB takes seconds, and doing that
   /// inside a frame is what froze the app on entering the tab. Callers draw the single-image path meanwhile.</summary>
   public ITexture FrameArrayTexture => _frameArrayTexture;

   /// <summary>Starts building <see cref="FrameArrayTexture"/> if it isn't built or being built already. Returns at once.</summary>
   public void RequestFrameArrayTexture(IResourceFactory factory)
   {
      if (!UseFrameArrayTextures) return;
      if (_frameArrayTexture != null || _rawBitmap == null || FrameCount <= 1) return;
      if (Interlocked.Exchange(ref _frameArrayRequested, 1) != 0) return;   // one build, however many images show it

      Task.Run(() =>
      {
         try
         {
            var layers = new byte[FrameCount][];
            for (uint i = 0; i < FrameCount; i++)
            {
               layers[i] = _rawBitmap.GetRawPixels(i);
               if (layers[i] == null) return;
            }

            var description = new TextureDescription
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
               ArrayLayers = FrameCount
            };

            _frameArrayTexture = factory.CreateTextureArray(description, layers);

            // The frames now live on the GPU, so every decoded copy on the CPU is dead weight - and there are two of
            // them per frame (the RGBA pixels and, for a GIF, the palette-index stream), which for a 200-frame 960x540
            // animation is hundreds of megabytes. Playback from here on only moves an index; nothing reads pixels.
            var released = _rawBitmap;
            foreach (var frame in _indexToFrame) frame.Value?.Dispose();
            _indexToFrame.Clear();
            released.ReleaseDecodedFrames();
         }
         catch
         {
            // A failed build must leave the animation on the single-image path, not kill the loader thread.
         }
      });
   }

   public BitmapFrame GetFrame(uint frameIndex)
   {
      if (_rawBitmap == null) return null;   // load still pending (async URI load) - nothing to hand back yet
      CurrentFrameIndex = frameIndex;
      var rawData = _rawBitmap.GetRawPixels(frameIndex);
      return new BitmapFrame(
         PixelWidth,
         PixelHeight,
         DpiXScale,
         DpiYScale,
         SurfaceLayout,
         rawData,
         frameIndex);
   }
   
   public BitmapFrame GetNextFrame()
   {
      if (CurrentFrameIndex >= EndFrame)
      {
         CurrentFrameIndex = StartFrame;
      }

      var frame = GetFrameFromCache(CurrentFrameIndex);

      CurrentFrameIndex++;
      return frame;
   }

   /// <summary>Moves the playback cursor one step and returns the frame it lands on - WITHOUT decoding anything. For an
   /// animation drawn from <see cref="FrameArrayTexture"/> the frame is a layer number, so asking the decoder for its
   /// pixels would rebuild hundreds of megabytes nobody reads.</summary>
   public uint AdvanceIndex(bool forward)
   {
      var shown = CurrentFrameIndex;

      if (forward)
      {
         if (CurrentFrameIndex >= EndFrame) shown = CurrentFrameIndex = StartFrame;
         CurrentFrameIndex++;
      }
      else
      {
         if (CurrentFrameIndex <= StartFrame) shown = CurrentFrameIndex = EndFrame;
         if (CurrentFrameIndex > 0) CurrentFrameIndex--;
      }

      return shown;
   }
   
   public BitmapFrame GetPreviousFrame()
   {
      if (CurrentFrameIndex <= StartFrame)
      {
         CurrentFrameIndex = EndFrame;
      }

      var frame = GetFrameFromCache(CurrentFrameIndex);
      
      CurrentFrameIndex--;
      return frame;
   }

   /// <summary>Decodes frames up to <paramref name="frameIndex"/> AHEAD of time. Despite the name it does the work
   /// SYNCHRONOUSLY on the calling thread and hands back a completed Task - awaiting it does not move anything off that
   /// thread. Playback does not need it (frames decode on demand); call it only to pay the cost somewhere deliberate.</summary>
   public Task DecodeFramesTillAsync(uint frameIndex)
   {
      if (frameIndex > FrameCount) frameIndex = FrameCount;
      
      for (uint i = 0; i < frameIndex; i++)
      {
         GetFrame(i);
      }

      return Task.CompletedTask;
   }

   public uint MipLevelsCount => _rawBitmap.MipLevelsCount;

   public bool HasMipLevels => MipLevelsCount > 0;

   public BitmapFrame GetMipLevel(uint level)
   {
      var mipData = _rawBitmap.GetMipLevelData(level);
      if (mipData == null) return null;
      
      return new BitmapFrame(
         mipData.Description.Width,
         mipData.Description.Height,
         DpiXScale,
         DpiYScale,
         SurfaceLayout,
         mipData.RawPixels,
         level);
   }

   private void Load(Uri uri)
   {
      var path = uri.OriginalString.Replace("file://", "");
      var rawImg = BitmapLoader.Load(path);
      FillData(rawImg);
      IsLoaded = true;
   }

   private void FillData(IRawBitmap rawBitmap)
   {
      PixelWidth = rawBitmap.Width;
      PixelHeight = rawBitmap.Height;
      SurfaceLayout = rawBitmap.PixelFormat;
      SetPixels(rawBitmap.GetRawPixels(0));
      _rawBitmap = rawBitmap;
   }

   /// <summary>The frame at <paramref name="index"/>, decoded on first ask and kept. Used while the frame array does not
   /// exist yet; once it does, playback is layer numbers and no frame is decoded at all.</summary>
   public BitmapFrame GetCachedFrame(uint index) => GetFrameFromCache(index);

   private BitmapFrame GetFrameFromCache(uint index)
   {
      //return GetFrame(index);
      if (!_indexToFrame.TryGetValue(index, out var frame))
      {
         frame = GetFrame(index);
         _indexToFrame[index] = frame;
         //AddToCache(frame);
      }

      // if (_indexToFrame.Count > FrameCacheLimit)
      // {
      //    RemoveCacheItems(FrameCacheLimit - (uint)_indexToFrame.Count);
      // }

      return frame;
   }
   
   protected override void ReleaseUnmanagedResources()
   {
      foreach (var bitmapFrame in _indexToFrame)
      {
         bitmapFrame.Value?.Dispose();
      }
      _indexToFrame.Clear();
      _frameArrayTexture?.Dispose();
      _frameArrayTexture = null;
      _rawBitmap?.ReleaseDecodedFrames();
      // ...and the base's own texture, which is where a still image's ENTIRE picture lives (77 MB for the 4984x3858 TGA).
      // Overriding this without calling it left that texture unreachable and unfreed: the only path to it was here.
      base.ReleaseUnmanagedResources();
   }

   private void AddToCache(BitmapFrame frame)
   {
      _framesCache.Enqueue(frame);
      if (_framesCache.Count > FrameCacheLimit)
      {
         var extraFrame = _framesCache.Dequeue();
         extraFrame.Dispose();
      }
   }

   private void RemoveCacheItems(uint number)
   {
      for (int i = 0; i < number; i++)
      {
         if (_framesCache.Count > 0)
         {
            var oldFrame = _framesCache.Dequeue();
            _indexToFrame.Remove(oldFrame.Index);
            oldFrame.Dispose();
         }
      }
   }
}