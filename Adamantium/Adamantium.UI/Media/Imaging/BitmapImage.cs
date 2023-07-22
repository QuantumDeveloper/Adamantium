using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Imaging;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media.Imaging;

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
      bitmap.GetFrameData(0))
   {
      _rawBitmap = bitmap;
   }

   public static readonly AdamantiumProperty UriSourceProperty = AdamantiumProperty.Register(nameof(UriSource),
      typeof(Uri), typeof(BitmapImage), new PropertyMetadata(null, UriChangedCallback));

   private static void UriChangedCallback(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
   {
      if (adamantiumObject is BitmapImage bitmap)
      {
         var uri = (Uri)e.NewValue;
         if (uri.IsFile)
         {
            bitmap.Load(uri);
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
         var oldLimit = (uint)e.OldValue;
         if (newLimit < oldLimit)
         {
            bitmap.RemoveCacheItems(newLimit - oldLimit);
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
   
   public bool IsLoaded { get; set; }

   public uint FrameCount => _rawBitmap.FramesCount;
   
   public uint StartFrame { get; set; }
   
   public uint EndFrame { get; set; }

   public BitmapFrame GetFrame(uint frameIndex)
   {
      CurrentFrameIndex = frameIndex;
      var rawData = _rawBitmap.GetFrameData(frameIndex);
      return new BitmapFrame(
         PixelWidth,
         PixelHeight,
         DpiXScale,
         DpiYScale,
         PixelFormat,
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
      var rawData = _rawBitmap.GetMipLevelData(level, out var mipDescription);
      if (rawData == null) return null;
      
      return new BitmapFrame(
         mipDescription.Width,
         mipDescription.Height,
         DpiXScale,
         DpiYScale,
         PixelFormat,
         rawData,
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
      PixelFormat = rawBitmap.PixelFormat;
      SetPixels(rawBitmap.GetFrameData(0));
      _rawBitmap = rawBitmap;
   }

   private BitmapFrame GetFrameFromCache(uint index)
   {
      if (!_indexToFrame.TryGetValue(index, out var frame))
      {
         frame = GetFrame(index);
         _indexToFrame[index] = frame;
         AddToCache(frame);
      }

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