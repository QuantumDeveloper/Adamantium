using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.RoutedEvents;
using Serilog;

namespace Adamantium.UI.Controls.Panels;

public class RenderTargetPanel: Grid
{
   private RenderTargetImage _currentRenderTarget;
   private RenderTargetImage _nextRenderTarget;

   public RenderTargetPanel()
   {
   }

   static RenderTargetPanel()
   {
      UseLayoutRoundingProperty.OverrideMetadata(typeof(RenderTargetPanel),
         new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure));
   }

   public static readonly AdamantiumProperty SurfaceFormatProperty = AdamantiumProperty.Register(nameof(SurfaceFormat),
      typeof(SurfaceFormat), typeof(RenderTargetPanel),
      new PropertyMetadata(SurfaceFormat.B8G8R8A8.UNorm, PropertyMetadataOptions.AffectsRender,
         RenderTargetParametersChanged));

   public static readonly AdamantiumProperty HandleProperty = AdamantiumProperty.RegisterReadOnly(nameof(Handle),
      typeof (IntPtr), typeof (RenderTargetPanel),
      new PropertyMetadata(IntPtr.Zero));

   public static readonly AdamantiumProperty PixelWidthProperty =
      AdamantiumProperty.RegisterReadOnly(nameof(PixelWidth), typeof(UInt32), typeof(RenderTargetPanel),
         new PropertyMetadata(0U, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty PixelHeightProperty =
      AdamantiumProperty.RegisterReadOnly(nameof(PixelHeight), typeof(UInt32), typeof(RenderTargetPanel),
         new PropertyMetadata(0U, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty RenderTargetProperty = 
      AdamantiumProperty.RegisterReadOnly(nameof(RenderTarget), typeof(RenderTargetImage), typeof(RenderTargetPanel),
         new PropertyMetadata(null, OnRenderTargetChanged));

   private static void OnRenderTargetChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is RenderTargetPanel panel && e.NewValue is RenderTargetImage)
      {
         panel.RaiseRenderTargetCreatedOrUpdatedEvent();
      }
   }

   private static void OnCanPresentChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is RenderTargetPanel panel && (bool)e.NewValue)
      {
         panel.InvalidateRender(false);
      }
   }

   public RenderTargetImage RenderTarget
   {
      get => GetValue<RenderTargetImage>(RenderTargetProperty); 
      private set => SetValue(RenderTargetProperty, value);
   }

   public SurfaceFormat SurfaceFormat
   {
      get => GetValue<SurfaceFormat>(SurfaceFormatProperty);
      set => SetValue(SurfaceFormatProperty, value);
   }
      
   public UInt32 PixelWidth
   {
      get => GetValue<UInt32>(PixelWidthProperty);
      set => SetValue(PixelWidthProperty, value);
   }

   public UInt32 PixelHeight
   {
      get => GetValue<UInt32>(PixelHeightProperty);
      set => SetValue(PixelHeightProperty, value);
   }
   
   public static readonly RoutedEvent RenderTargetCreatedOrUpdatedEvent = 
      EventManager.RegisterRoutedEvent(nameof(RenderTargetCreatedOrUpdated),
         RoutingStrategy.Direct, typeof(RenderTargetChangedEventHandler), typeof(RenderTargetPanel));
    
   public event RenderTargetChangedEventHandler RenderTargetCreatedOrUpdated
   {
      add => AddHandler(RenderTargetCreatedOrUpdatedEvent, value);
      remove => RemoveHandler(RenderTargetCreatedOrUpdatedEvent, value);
   }

   private static void RenderTargetParametersChanged(AdamantiumComponent adamantiumComponent, AdamantiumPropertyChangedEventArgs e)
   {
      var image = adamantiumComponent as RenderTargetPanel;
      image?.UpdateOrCreateRenderTarget();
   }

   protected override void OnSizeChanged(SizeChangedEventArgs e)
   {
      if (e.OriginalSource == this)
      {
         Log.Logger.Debug($"Size changed event. New size: {e.NewSize}");
         UpdateOrCreateRenderTarget();
         sizeChanged = true;
      }
   }

   /// <summary>
   /// Handle to the render target texture
   /// </summary>
   public IntPtr Handle
   {
      get => GetValue<IntPtr>(HandleProperty);
      private set => SetValue(HandleProperty, value);
   }

   private void UpdateOrCreateRenderTarget()
   {
      try
      {
         if ((int)ActualWidth <= 0 || (int)ActualHeight <= 0 || !IsAttachedToVisualTree) return;
         
         // here we need to multiply on current DPI
         PixelWidth = (uint)ActualWidth;
         PixelHeight = (uint)ActualHeight;
         
         _nextRenderTarget = new RenderTargetImage(PixelWidth, PixelHeight, MSAALevel.None, SurfaceFormat);
         _nextRenderTarget.GetOrCreateTexture(UIAppContext.Current.GraphicsContext.GetResourceFactory());
         unsafe
         {
            Handle = new IntPtr(_nextRenderTarget.NativePointer);
         }
         RenderTarget = _nextRenderTarget;
      }
      catch (Exception e)
      {
         Log.Error(e.ToString());
         throw;
      }
   }

   private void RaiseRenderTargetCreatedOrUpdatedEvent()
   {
      var pixelWidth = PixelWidth;
      var pixelHeight = PixelHeight;
      var args = new RenderTargetEventArgs(_nextRenderTarget, pixelWidth, pixelHeight, SurfaceFormat);
      args.RoutedEvent = RenderTargetCreatedOrUpdatedEvent;
      RaiseEvent(args);
   }

   protected override void OnInitialized()
   {
      base.OnInitialized();
      UpdateOrCreateRenderTarget();
   }

   protected override void OnRender(IDrawingContext context)
   {
      UpdateOrCreateRenderTarget();
      _currentRenderTarget?.Dispose();
      _currentRenderTarget = _nextRenderTarget;
      unsafe
      {
         Handle = _currentRenderTarget == null ? IntPtr.Zero : new IntPtr(_currentRenderTarget.NativePointer);
      }
      //context.ForControl(this).DrawImage(_currentRenderTarget, Background, new Rect(new Size(ActualWidth, ActualHeight)), CornerRadius.Empty);
      context.ForControl(this)
         .DrawRectangle(Background, new Rect(new Size(ActualWidth, ActualHeight)), CornerRadius.Empty);
   }

   public async Task SaveCurrentFrame(Uri path, ImageFileType fileType)
   {
      await Task.Run(() => _currentRenderTarget.Save(path, fileType));
   }
}