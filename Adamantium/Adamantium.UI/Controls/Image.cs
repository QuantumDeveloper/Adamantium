using System;
using System.Threading.Tasks;
using System.Timers;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Threading;
using Timer = System.Timers.Timer;

namespace Adamantium.UI.Controls;

public class Image : InputUIComponent
{
   private Timer _timer;
   private BitmapImage _bitmap;
   private BitmapFrame _frame;
   private BitmapFrame _oldFrame;
   private UInt64 _currentReplayIteration;
   
   public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
      typeof(Stretch), typeof(Image), new PropertyMetadata(Stretch.Uniform, PropertyMetadataOptions.AffectsMeasure));
   
   public static readonly AdamantiumProperty StretchDirectionProperty = AdamantiumProperty.Register(nameof(StretchDirection),
      typeof(StretchDirection), typeof(Image), new PropertyMetadata(StretchDirection.Both, PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty SourceProperty = AdamantiumProperty.Register(nameof(Source),
      typeof(ImageSource), typeof(Image),
      new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnSourceChanged));

   public static readonly AdamantiumProperty FilterBrushProperty = AdamantiumProperty.Register(nameof(FilterBrush),
      typeof(Brush), typeof(Image), new PropertyMetadata(Brushes.White, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
      typeof(CornerRadius), typeof(Image),
      new PropertyMetadata(new CornerRadius(0), PropertyMetadataOptions.AffectsRender));
   
   public static readonly AdamantiumProperty DelayProperty = AdamantiumProperty.Register(nameof(Delay),
      typeof(UInt32), typeof(Image),
      new PropertyMetadata(16U, OnDelayChanged));
   
   public static readonly AdamantiumProperty NumberOfReplaysProperty = AdamantiumProperty.Register(nameof(NumberOfReplays),
      typeof(UInt64), typeof(Image),
      new PropertyMetadata(UInt64.MaxValue, OnNumberOfReplaysChanged));
   
   public static readonly AdamantiumProperty StartFrameProperty = AdamantiumProperty.Register(nameof(StartFrame),
      typeof(UInt32), typeof(Image),
      new PropertyMetadata(0U, OnFrameRangeChanged));
   
   public static readonly AdamantiumProperty EndFrameProperty = AdamantiumProperty.Register(nameof(EndFrame),
      typeof(UInt32), typeof(Image),
      new PropertyMetadata(UInt32.MaxValue, OnFrameRangeChanged));
   
   public static readonly AdamantiumProperty ReplayDirectionProperty = AdamantiumProperty.Register(nameof(ReplayDirection),
      typeof(ReplayDirection), typeof(Image),
      new PropertyMetadata(ReplayDirection.Forward, OnReplayDirectionChanged));
   
   public static readonly AdamantiumProperty MipLevelProperty = AdamantiumProperty.Register(nameof(MipLevel),
      typeof(UInt32), typeof(Image),
      new PropertyMetadata(0U, OnMipLevelChangedCallback));

   private static void OnMipLevelChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is Image img)
      {
         img.ProcessImageSource();
      }
   }

   private static void OnReplayDirectionChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is Image img)
      {
         if (img.ReplayDirection == ReplayDirection.ForwardLooped)
         {
            img._replayDirection = ReplayDirection.Forward;
         }
         else if (img.ReplayDirection == ReplayDirection.BackwardLooped)
         {
            img._replayDirection = ReplayDirection.Backward;
         }
      }
   }

   private static void OnFrameRangeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is Image img)
      {
         img.RestartTimer(img.Delay);
      }
   }

   private static void OnNumberOfReplaysChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is Image img)
      {
         img._currentReplayIteration = 0;
      }
   }

   private static void OnDelayChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is Image img)
      {
         img.RestartTimer((UInt32)e.NewValue);
      }
   }
   
   private static void OnSourceChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is Image img)
      {
         img.ProcessImageSource();
      }
   }
   
   public UInt32 MipLevel
   {
      get => GetValue<UInt32>(MipLevelProperty);
      set => SetValue(MipLevelProperty, value);
   }
   
   public UInt32 StartFrame
   {
      get => GetValue<UInt32>(StartFrameProperty);
      set => SetValue(StartFrameProperty, value);
   }
   
   public UInt32 EndFrame
   {
      get => GetValue<UInt32>(EndFrameProperty);
      set => SetValue(EndFrameProperty, value);
   }

   public ReplayDirection ReplayDirection
   {
      get => GetValue<ReplayDirection>(ReplayDirectionProperty);
      set => SetValue(ReplayDirectionProperty, value);
   }
   
   public UInt64 NumberOfReplays
   {
      get => GetValue<UInt64>(NumberOfReplaysProperty);
      set => SetValue(NumberOfReplaysProperty, value);
   }

   public UInt32 Delay
   {
      get => GetValue<UInt32>(DelayProperty);
      set => SetValue(DelayProperty, value);
   }

   public CornerRadius CornerRadius
   {
      get => GetValue<CornerRadius>(CornerRadiusProperty);
      set => SetValue(CornerRadiusProperty, value);
   }

   public Brush FilterBrush
   {
      get => GetValue<Brush>(FilterBrushProperty);
      set => SetValue(FilterBrushProperty, value);
   }

   public Stretch Stretch
   {
      get => GetValue<Stretch>(StretchProperty);
      set => SetValue(StretchProperty, value);
   }

   public StretchDirection StretchDirection
   {
      get => GetValue<StretchDirection>(StretchDirectionProperty);
      set => SetValue(StretchDirectionProperty, value);
   }

   public ImageSource Source
   {
      get => GetValue<BitmapSource>(SourceProperty);
      set => SetValue(SourceProperty, value);
   }

   public Image()
   {
   }

   private async void ProcessImageSource()
   {
      if (Source is BitmapImage bitmap)
      {
         _bitmap = bitmap;
         await DecodeBitmapFramesAsync(EndFrame);
         var endFrame = EndFrame > _bitmap.FrameCount ? _bitmap.FrameCount : EndFrame;
         if (StartFrame > endFrame)
         {
            StartFrame = endFrame;
         }
         
         _bitmap.StartFrame = StartFrame;
         _bitmap.EndFrame = endFrame;
         _bitmap.ResetFrameIndex();
         
         if (_bitmap.FrameCount > 1)
         {
            RestartTimer(Delay);
         }
         else
         {
            _frame = _bitmap.HasMipLevels && MipLevel > 0 ? _bitmap.GetMipLevel(MipLevel) : _bitmap.GetFrame(0);
         }
      }
   }

   private async Task DecodeBitmapFramesAsync(uint endFrame)
   {
      await _bitmap.DecodeFramesTillAsync(endFrame);
   }

   private void RestartTimer(uint delay)
   {
      _timer?.Stop();
      
      if (_bitmap == null || _bitmap.FrameCount == 1) return;
      
      _timer = new Timer(TimeSpan.FromMilliseconds(delay));
      _timer.Elapsed += TimerOnElapsed;
      _timer.Start();
   }

   private ReplayDirection _replayDirection;
   private void TimerOnElapsed(object sender, ElapsedEventArgs e)
   {
      if (NumberOfReplays == 0 || _currentReplayIteration >= NumberOfReplays)
      {
         _timer.Stop();
      }
      
      Dispatcher.CurrentDispatcher.Invoke(() =>
      {
         _oldFrame = _frame;
         switch (ReplayDirection)
         {
            case ReplayDirection.Forward:
               _frame = _bitmap.GetNextFrame();
               break;
            case ReplayDirection.Backward:
               _frame = _bitmap.GetPreviousFrame();
               break;
            case ReplayDirection.ForwardLooped:
               switch (_replayDirection)
               {
                  case ReplayDirection.Forward:
                  {
                     _frame = _bitmap.GetNextFrame();
                     if (_bitmap.CurrentFrameIndex >= _bitmap.EndFrame)
                     {
                        _replayDirection = ReplayDirection.Backward;
                     }

                     break;
                  }
                  case ReplayDirection.Backward:
                  {
                     _frame = _bitmap.GetPreviousFrame();
                     if (_bitmap.CurrentFrameIndex <= _bitmap.StartFrame)
                     {
                        _replayDirection = ReplayDirection.Forward;
                     }

                     break;
                  }
               }

               break;
            default:
            {
               switch (_replayDirection)
               {
                  case ReplayDirection.Backward:
                  {
                     _frame = _bitmap.GetPreviousFrame();
                     if (_bitmap.CurrentFrameIndex <= _bitmap.StartFrame)
                     {
                        _replayDirection = ReplayDirection.Forward;
                     }

                     break;
                  }
                  case ReplayDirection.Forward:
                  {
                     _frame = _bitmap.GetNextFrame();
                     if (_bitmap.CurrentFrameIndex >= _bitmap.EndFrame)
                     {
                        _replayDirection = ReplayDirection.Backward;
                     }

                     break;
                  }
               }

               break;
            }
         }

         if (_bitmap.CurrentFrameIndex >= _bitmap.FrameCount)
         {
            _currentReplayIteration++;
         }
         InvalidateRender(false);
      });
   }
   
   protected override Size MeasureOverride(Size availableSize)
   {
      if (Source != null)
      {
         var source = Source;

         var size = CalculateScaling(Stretch, availableSize, new Size(source.Width, source.Height));
         return size;
      }

      return Size.Zero;
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      if (Source != null)
      {
         var source = Source;

         return CalculateScaling(Stretch, finalSize, new Size(source.Width, source.Height));
      }

      return Size.Zero;
   }

   protected override void OnRender(DrawingContext context)
   {
      base.OnRender(context);
      if (Source == null) return;
      
      if (_frame is { IsDisposed: false })
      {
         //_oldFrame?.Dispose();
         
         //context.DrawRectangle(Brushes.Red, new Rect(Bounds.Size), CornerRadius);
         context.DrawImage(_frame, FilterBrush, new Rect(Bounds.Size), CornerRadius);
      }
      else
      {
         context.DrawImage(Source, FilterBrush, new Rect(Bounds.Size), CornerRadius);
      }
   }

   public Size CalculateScaling(Stretch stretch, Size destinationSize, Size sourceSize)
   {
      double sizeX = sourceSize.Width;
      double sizeY = sourceSize.Height;
      
      var scaleX = 1.0;
      var scaleY = 1.0;
      
      bool isConstrainedWidth = !double.IsPositiveInfinity(destinationSize.Width);
      bool isConstrainedHeight = !double.IsPositiveInfinity(destinationSize.Height);

      if (stretch != Stretch.None && (isConstrainedWidth || isConstrainedHeight))
      {
         scaleX = sizeX == 0 ? 0.0 : destinationSize.Width / sourceSize.Width;
         scaleY = sizeX == 0 ? 0.0 : destinationSize.Height / sourceSize.Height;

         if (!isConstrainedWidth)
         {
            scaleX = scaleY;
         }
         else if (!isConstrainedHeight)
         {
            scaleY = scaleX;
         }
         else
         {
            // If not preserving aspect ratio, then just apply transform to fit
            switch (stretch)
            {
               case Stretch.Uniform:
                  // Find minimum scale that we use for both axes
                  double minscale = scaleX < scaleY ? scaleX : scaleY;
                  scaleX = scaleY = minscale;
                  break;

               case Stretch.UniformToFill:
                  // Find maximum scale that we use for both axes
                  double maxscale = scaleX > scaleY ? scaleX : scaleY;
                  scaleX = scaleY = maxscale;
                  break;

               case Stretch.Fill:
                  // We already computed the fill scale factors above, so just use them
                  break;
            }
         }

         // Apply stretch direction by bounding scales.
         // In the uniform case, scaleX=scaleY, so this sort of clamping will maintain aspect ratio
         // In the uniform fill case, we have the same result too.
         // In the fill case, note that we change aspect ratio, but that is okay
         switch (StretchDirection)
         {
            case StretchDirection.UpOnly:
               if (scaleX < 1.0)
                  scaleX = 1.0;
               if (scaleY < 1.0)
                  scaleY = 1.0;
               break;

            case StretchDirection.DownOnly:
               if (scaleX > 1.0)
                  scaleX = 1.0;
               if (scaleY > 1.0)
                  scaleY = 1.0;
               break;

            case StretchDirection.Both:
               break;

            case StretchDirection.None:
               scaleX = scaleY = 1;
               break;
         }
      }

      return new Size( sizeX * scaleX,  sizeY * scaleY);
   }
}