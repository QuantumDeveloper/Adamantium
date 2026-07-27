using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

public class Image : InputUIComponent, IDesignTimeAnimatedMedia
{
   private bool _runtimePlaying;
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
      // AffectsMeasure too: a NON-BitmapImage source (a RenderTargetImage / a VisualRenderer bitmap) has its size known
      // immediately but ProcessImageSource only re-measures for the async BitmapImage path - without this the Image keeps
      // the size it had while Source was null (0x0) and draws nothing. Source drives the desired size, so it must measure.
      new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender, OnSourceChanged));

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
         img.InvalidateRender(false);
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
         img.InvalidateRender(false);
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

         // Wait for the URI's background load (BitmapImage loads off the UI thread) so decoding never freezes the UI.
         await bitmap.EnsureLoadedAsync();
         if (!ReferenceEquals(_bitmap, bitmap)) return;   // Source changed mid-load -> this result is stale

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
            // In the headless designer there is no real clock, so register with the design-time clock the live previewer
            // ticks (a static shot leaves it un-ticked -> frame 0). At runtime, register the frame ticker with the
            // loop-thread heartbeat (AnimationManager) - marshalled via Dispatcher.Post because this async continuation
            // may resume on a thread-pool thread and AnimationManager, like the render loop, is single-threaded.
            if (Design.IsDesignMode)
               DesignTimeMediaClock.Register(this);
            else
               UIAppContext.Current.Dispatcher.Post(StartRuntimePlayback);
         }
         else
         {
            _frame = _bitmap.HasMipLevels && MipLevel > 0 ? _bitmap.GetMipLevel(MipLevel) : _bitmap.GetFrame(0);
         }

         // The async load can finish AFTER the Source-change layout/render already ran (measuring/painting nothing);
         // re-measure (auto-sized images pick up the real size) and repaint now that the frame exists.
         InvalidateMeasure();
         InvalidateRender(false);
      }
   }

   private async Task DecodeBitmapFramesAsync(uint endFrame)
   {
      await _bitmap.DecodeFramesTillAsync(endFrame);
   }

   protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnAttachedToVisualTree(e);
      // Resume playback after a re-attach (tab switch, virtualization recycle): the ticker self-removed on detach. Post
      // runs inline when already on the loop thread (attach normally runs during the loop's layout pass).
      UIAppContext.Current.Dispatcher.Post(StartRuntimePlayback);
   }

   // Runtime frame-based playback rides the per-frame loop heartbeat (AnimationManager, on the render-loop thread),
   // replacing a background System.Timers.Timer whose per-frame completion hopped through Dispatcher.Invoke onto the Win32
   // MESSAGE-PUMP thread. That hop ran only when the pump woke (on input), and the dispatcher's coalescing signal woke it
   // for just the FIRST queued image - so animations froze until the mouse moved and only ONE image advanced. It also ran
   // AdvanceFrame (mutating _frame) off the loop thread while the render read it. Registered when an animated source loads
   // (ProcessImageSource) and on re-attach (OnAttachedToVisualTree), both via Dispatcher.Post onto the loop thread; the
   // ticker self-removes when the source stops being animated or the image detaches.
   private void StartRuntimePlayback()
   {
      if (_runtimePlaying || Design.IsDesignMode || _bitmap is not { FrameCount: > 1 }) return;
      _runtimePlaying = true;
      _playbackElapsedMs = 0;
      AnimationManager.AddTicker(RuntimeTick);
   }

   private ReplayDirection _replayDirection;

   // One heartbeat tick of runtime playback. Returns true (done -> the heartbeat drops this ticker) when the source is no
   // longer animated or the image has left the tree; a hidden-but-attached image keeps the ticker but does not advance.
   private bool _playbackFailed;

   private bool RuntimeTick(double deltaSeconds)
   {
      if (_playbackFailed || !IsAttachedToVisualTree || _bitmap is not { FrameCount: > 1 })
      {
         _runtimePlaying = false;
         return true;
      }
      if (Visibility != Visibility.Visible) return false;
      var keepPlaying = AdvancePlayback(deltaSeconds);
      if (!keepPlaying) _runtimePlaying = false;
      return !keepPlaying;
   }

   // Advances _frame by one step in the current ReplayDirection. Shared by the runtime timer and the design-time clock.
   private void AdvanceFrame()
   {
      _oldFrame = _frame;
      try
      {
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
      }
      catch (Exception)
      {
         // A frame that cannot be produced will not start producing itself on the next tick, so STOP instead of failing
         // sixty times a second - swallowing silently is what let a null frame cache freeze every animation unnoticed.
         // Letting it escape is not an option either: the heartbeat has no guard, so one bad image would kill every
         // running animation.
         _playbackFailed = true;
      }
   }

   private double _playbackElapsedMs;

   // Advance whole frames by elapsed time and invalidate directly on the calling (loop) thread - shared by the runtime
   // heartbeat ticker (RuntimeTick) and the design-time clock (AdvanceDesignTime). Accumulate the delta, advance whole
   // frames at the configured Delay, then repaint. Keeps playing while replays remain (loops forever by default); the
   // previewer caps total captured frames.
   private bool AdvancePlayback(double deltaSeconds)
   {
      if (_bitmap is not { FrameCount: > 1 }) return false;

      _playbackElapsedMs += deltaSeconds * 1000.0;
      var delay = Math.Max(1u, Delay);
      var advanced = false;
      while (_playbackElapsedMs >= delay)
      {
         _playbackElapsedMs -= delay;
         AdvanceFrame();
         advanced = true;
      }

      if (advanced) InvalidateRender(false);
      return NumberOfReplays == UInt64.MaxValue || _currentReplayIteration <= NumberOfReplays;
   }

   // Design-time playback: the live previewer ticks this with virtual time instead of the runtime heartbeat (see
   // IDesignTimeAnimatedMedia / DesignTimeMediaClock). Same advance logic as runtime.
   public bool AdvanceDesignTime(double deltaSeconds) => AdvancePlayback(deltaSeconds);

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

   protected override void OnRender(IDrawingContext context)
   {
      base.OnRender(context);
      if (Source == null) return;

      // Draw the current animation frame, not the static source: each frame is its own BitmapSource (texture cached
      // per frame index), so advancing _frame makes animated images play. _frame is null for non-bitmap sources
      // (RenderTargetImage / SharedSurfaceImage) and before the first tick, where we draw the source directly.
      ImageSource image = _frame ?? Source;

      // A URI-sourced bitmap now loads asynchronously (off the UI thread). Until it finishes it has no pixel data, so
      // rendering it would build a texture from null and crash. Skip until it is ready - ProcessImageSource sets _frame
      // and re-invalidates once loaded. A decoded _frame (BitmapFrame) or a non-BitmapImage source is always ready.
      if (image is BitmapImage { IsLoaded: false }) return;

      context.ForControl(this).DrawImage(image, FilterBrush, new Rect(Bounds.Size), CornerRadius);
   }

   private Size CalculateScaling(Stretch stretch, Size destinationSize, Size sourceSize)
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