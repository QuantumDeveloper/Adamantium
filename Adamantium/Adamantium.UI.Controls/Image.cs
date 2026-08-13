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
   private uint _layerIndex;      // frame being shown when drawing from the frame array (no BitmapFrame involved)
   private uint _frameCursor;     // THIS control's playback position - the source may be shared with other images
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

   public static readonly AdamantiumProperty IsPlayingProperty = AdamantiumProperty.Register(nameof(IsPlaying),
      typeof(bool), typeof(Image), new PropertyMetadata(true, OnIsPlayingChanged));

   // Read-only: how many frames the SOURCE turned out to have. Nobody can know it before the decode finishes, and a UI
   // that lets you pick a frame range needs it to bound the choice.
   public static readonly AdamantiumProperty FrameCountProperty = AdamantiumProperty.RegisterReadOnly(nameof(FrameCount),
      typeof(UInt32), typeof(Image), new PropertyMetadata(0U));

   private static void OnIsPlayingChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      // Resuming has to be able to WAKE the ticker: it removes itself while paused only if the animation also ended,
      // and either way starting it again is harmless (StartRuntimePlayback returns if it is already running).
      if (a is Image img && (bool)e.NewValue) img.StartRuntimePlayback();
   }

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

   // The range is not just a hint for the next load: it drives playback LIVE, so moving it re-aims the loop at once and
   // pulls the current frame inside the new bounds (otherwise the cursor could sit outside and the animation would jump
   // to the start on the next step).
   private static void OnFrameRangeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is not Image img) return;

      img.ApplyFrameRange();
      img.InvalidateRender(false);
   }

   private void ApplyFrameRange()
   {
      if (_bitmap is not { FrameCount: > 1 }) return;

      var (start, end) = FrameRange();
      if (_frameCursor < start) _frameCursor = start;
      else if (_frameCursor > end) _frameCursor = end;
      _layerIndex = _frameCursor;
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
      if (a is not Image img) return;

      img.WatchDrawing(e.OldValue, e.NewValue);
      img.ProcessImageSource();
   }

   // A DRAWING source only. It is MUTABLE and nobody else is watching it: the property system re-renders when a
   // BRUSH-valued property changes, but Source is an ImageSource, so recolouring a shape three levels down inside the
   // drawing would otherwise never reach this element. Every other source kind is immutable and needs none of this.
   private void WatchDrawing(object oldSource, object newSource)
   {
      if (oldSource is DrawingImage previous)
      {
         previous.Changed -= OnDrawingChanged;
      }

      if (newSource is DrawingImage current)
      {
         current.Changed += OnDrawingChanged;
      }
   }

   // A drawing changing is a SHAPE change, not a recolour of the same commands - the replay emits different geometry -
   // so this re-records rather than re-baking the paint.
   private void OnDrawingChanged(object sender, EventArgs e) => InvalidateRender(false);

   // A drawing lives in a RESOURCE, outside the tree, so its bindings have nothing to resolve against until it is hung
   // on an element that does. Done at the point of use rather than on attach: when this element attaches, the ancestor
   // chain above it is not finished yet, and an inherited DataContext arriving later notifies nobody. Cheap - it is a
   // no-op once the owner and its data stop changing.
   private void EnsureDrawingAttached()
   {
      if (Source is DrawingImage drawing) drawing.Attach(this);
   }
   
   public UInt32 MipLevel
   {
      get => GetValue<UInt32>(MipLevelProperty);
      set => SetValue(MipLevelProperty, value);
   }

   /// <summary>Whether an animated source is running. Setting it false STOPS on the current frame and setting it true
   /// carries on from there - the cursor is untouched either way.</summary>
   public bool IsPlaying
   {
      get => GetValue<bool>(IsPlayingProperty);
      set => SetValue(IsPlayingProperty, value);
   }

   /// <summary>Frames in the current source (1 for a still image, 0 until it has loaded).</summary>
   public UInt32 FrameCount
   {
      get => GetValue<UInt32>(FrameCountProperty);
      private set => SetValue(FrameCountProperty, value);
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
      // ImageSource, not BitmapSource: every source used to be a bitmap, so the narrower read never showed -
      // DrawingImage is the first that is not one, and reading it as a BitmapSource yields nothing at all.
      get => GetValue<ImageSource>(SourceProperty);
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

         // Frames are decoded ON DEMAND - GetFrameFromCache decodes and caches a frame the first time it is asked for.
         // Decoding every frame here only LOOKED asynchronous: DecodeFramesTillAsync runs a plain loop and hands back an
         // already-completed Task, so the await continued INLINE, on whatever thread set Source - which for anything that
         // is not a URI (a drop, a stream) is the loop thread that owns layout. A 200-frame GIF therefore froze the whole
         // UI, scrolling included, for as long as it took to decode all of it - and decoded frames nobody may ever see.
         FrameCount = _bitmap.FrameCount;
         _frameCursor = FrameRange().Start;
         _layerIndex = _frameCursor;

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
      // Paused: hold the ticker but let no time accumulate, so resuming continues from this frame instead of jumping
      // ahead by however long the pause lasted.
      if (!IsPlaying) return false;
      var keepPlaying = AdvancePlayback(deltaSeconds);
      if (!keepPlaying) _runtimePlaying = false;
      return !keepPlaying;
   }

   // The frame range this control plays, clamped to what the source has. Read per step rather than stored, so moving a
   // slider takes effect on the very next frame.
   private (uint Start, uint End) FrameRange()
   {
      var last = _bitmap.FrameCount > 0 ? _bitmap.FrameCount - 1 : 0;
      var start = Math.Min(StartFrame, last);
      var end = Math.Min(EndFrame, last);
      return end < start ? (start, start) : (start, end);
   }

   // Advances the CONTROL's own cursor by one step in the current ReplayDirection. The cursor lives here, not in the
   // BitmapImage, because one decoded source can be shown by several images at once - each with its own range, speed and
   // position. Sharing the source is what stops the same file being decoded (and uploaded) once per image.
   private void AdvanceFrame()
   {
      var (start, end) = FrameRange();
      if (_frameCursor < start || _frameCursor > end) _frameCursor = start;

      var backwards = ReplayDirection is ReplayDirection.Backward
                      || (ReplayDirection is ReplayDirection.ForwardLooped or ReplayDirection.BackwardLooped
                          && _replayDirection == ReplayDirection.Backward);

      if (backwards)
      {
         if (_frameCursor <= start)
         {
            // Bounce for a ping-pong direction, wrap for a plain backward one.
            if (ReplayDirection is ReplayDirection.ForwardLooped or ReplayDirection.BackwardLooped)
            {
               _replayDirection = ReplayDirection.Forward;
               if (_frameCursor < end) _frameCursor++;
            }
            else
            {
               _frameCursor = end;
               _currentReplayIteration++;
            }
         }
         else
         {
            _frameCursor--;
         }
      }
      else
      {
         if (_frameCursor >= end)
         {
            if (ReplayDirection is ReplayDirection.ForwardLooped or ReplayDirection.BackwardLooped)
            {
               _replayDirection = ReplayDirection.Backward;
               if (_frameCursor > start) _frameCursor--;
            }
            else
            {
               _frameCursor = start;
               _currentReplayIteration++;
            }
         }
         else
         {
            _frameCursor++;
         }
      }

      _layerIndex = _frameCursor;

      // Drawing from the frame array? Then the step IS the number - asking the decoder for pixels would rebuild data the
      // GPU already holds, which is exactly what the array was built to stop paying for.
      if (_bitmap.FrameArrayTexture != null) return;

      _oldFrame = _frame;
      try
      {
         _frame = _bitmap.GetCachedFrame(_frameCursor);
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
      // Before the size is read: a drawing source's extent can itself come from bindings inside the drawing.
      EnsureDrawingAttached();

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
      if (Source == null) return Size.Zero;

      // A Stretch alignment means "take the slot" - so take it, and CENTRE the fitted picture inside it (OnRender).
      // Returning only the fitted size made the element smaller than the slot it was given, and the base arrange anchors
      // Stretch at the start, so the whole leftover piled up on ONE side: a picture whose aspect differs from its slot
      // sat against the left/top edge with an empty strip on the right/bottom. Any other alignment still shrinks to the
      // picture - that is the size THAT alignment then positions.
      var fitted = CalculateScaling(Stretch, finalSize, new Size(Source.Width, Source.Height));

      return new Size(
         HorizontalAlignment == HorizontalAlignment.Stretch ? finalSize.Width : fitted.Width,
         VerticalAlignment == VerticalAlignment.Stretch ? finalSize.Height : fitted.Height);
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

      // Scale the picture per Stretch, then CENTRE it in what the element occupies. Two directions to the leftover:
      // a fit SMALLER than the element leaves an equal margin on both sides (Uniform in a box of another aspect);
      // a fit LARGER is CROPPED, equally on both sides, by drawing only the part of the source that stays inside -
      // which is what makes None (1:1, cropped), UniformToFill (fills, crops the long axis) and Fill (squashes to
      // the box) three different pictures. Squeezing the whole source into the element instead made all three the
      // same drawing: Fill.
      var fitted = CalculateScaling(Stretch, Bounds.Size, new Size(Source.Width, Source.Height));
      var visibleU = fitted.Width > 0 ? Math.Min(1.0, Bounds.Width / fitted.Width) : 1.0;
      var visibleV = fitted.Height > 0 ? Math.Min(1.0, Bounds.Height / fitted.Height) : 1.0;
      var width = Math.Min(fitted.Width, Bounds.Width);
      var height = Math.Min(fitted.Height, Bounds.Height);
      var destination = new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);

      var session = context.ForControl(this);

      // A DRAWING source draws itself: its shapes are replayed into this session through the viewbox-to-destination
      // mapping, so it stays sharp at any size and nothing is rasterised. The cropping the raster paths do below has
      // nothing to crop here - the fitted rect already carries the Stretch decision.
      if (image is DrawingImage drawing)
      {
         EnsureDrawingAttached();
         drawing.Render(session, destination);
         return;
      }

      // An animation draws from ONE texture whose layers are its frames: the frame is a number handed to the shader, so
      // advancing it costs no upload and no allocation. (Before this, every frame became its own texture - a 200-frame
      // GIF meant 200 of them, ~400 MB, built one blocking upload at a time.) The layer is the frame's OWN index, not
      // the bitmap's cursor: the cursor has already moved on to the next frame by the time this one is drawn.
      if (BitmapImage.UseFrameArrayTextures && _bitmap is { FrameCount: > 1, IsLoaded: true } animated)
      {
         // Which frame: once the array exists, the layer the cursor last landed on (no BitmapFrame is decoded at all).
         // Until then the fallback path is decoding frames, so follow ITS frame; before the first tick, the range start.
         var layer = animated.FrameArrayTexture != null ? _layerIndex : _frame?.Index ?? animated.StartFrame;
         session.DrawImageFrame(animated, FilterBrush, destination, CornerRadius, (int)layer);
         return;
      }

      if (visibleU >= 1 && visibleV >= 1)
      {
         session.DrawImage(image, FilterBrush, destination, CornerRadius);
         return;
      }

      session.DrawImage(image, FilterBrush, destination, CornerRadius,
         new Rect((1 - visibleU) / 2, (1 - visibleV) / 2, visibleU, visibleV));
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