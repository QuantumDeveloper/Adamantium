using Adamantium.Imaging;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media.Imaging;

public sealed class BitmapImage : BitmapSource
{
   private IRawBitmap _rawBitmap;
   private Queue<BitmapFrame> _framesCache;
   private Dictionary<uint, BitmapFrame> _indexToFrame;
   public BitmapImage()
   {
      _framesCache = new Queue<BitmapFrame>();
      _indexToFrame = new Dictionary<uint, BitmapFrame>();
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
      _framesCache = new Queue<BitmapFrame>();
      _indexToFrame = new Dictionary<uint, BitmapFrame>();
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
         var newLimit = (uint)e.NewValue;
         if (e.OldValue != AdamantiumProperty.UnsetValue)
         {
            var oldLimit = (uint)e.OldValue;
            if (newLimit < oldLimit)
            {
               bitmap.RemoveCacheItems(newLimit - oldLimit);
            }
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