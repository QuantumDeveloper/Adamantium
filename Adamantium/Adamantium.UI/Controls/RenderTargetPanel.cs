using System;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.RoutedEvents;
using Serilog;

namespace Adamantium.UI.Controls;

public unsafe class RenderTargetPanel: Grid
{
   private RenderTargetImage _renderTarget;
   
   public RenderTargetPanel() { }

   static RenderTargetPanel()
   {
      UseLayoutRoundingProperty.OverrideMetadata(typeof(RenderTargetPanel),
         new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure));
   }

   public static readonly AdamantiumProperty PixelFormatProperty = AdamantiumProperty.Register(nameof(PixelFormat),
      typeof(SurfaceFormat), typeof(RenderTargetPanel),
      new PropertyMetadata(SurfaceFormat.R8G8B8A8.UNorm, PropertyMetadataOptions.AffectsRender,
         RenderTargetParametersChanged));

   public static readonly AdamantiumProperty HandleProperty = AdamantiumProperty.RegisterReadOnly(nameof(Handle),
      typeof (IntPtr), typeof (RenderTargetPanel),
      new PropertyMetadata(IntPtr.Zero, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty PixelWidthProperty =
      AdamantiumProperty.RegisterReadOnly(nameof(PixelWidth), typeof(Int32), typeof(RenderTargetPanel),
         new PropertyMetadata(0));

   public static readonly AdamantiumProperty PixelHeightProperty =
      AdamantiumProperty.RegisterReadOnly(nameof(PixelHeight), typeof(Int32), typeof(RenderTargetPanel),
         new PropertyMetadata(0));

   public SurfaceFormat PixelFormat
   {
      get => GetValue<SurfaceFormat>(PixelFormatProperty);
      set => SetValue(PixelFormatProperty, value);
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

   private static void RenderTargetParametersChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
   {
      var image = adamantiumObject as RenderTargetPanel;
      image?.UpdateOrCreateRenderTarget();
   }

   protected override void OnSizeChanged(SizeChangedEventArgs e)
   {
      UpdateOrCreateRenderTarget();
      sizeChanged = true;
   }

   /// <summary>
   /// Handle to the rendertarget texture
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
         if ((int)ActualWidth <= 0 || (int)ActualHeight <= 0) return;
         
         PixelWidth = (uint)ActualWidth;
         PixelHeight = (uint)ActualHeight;
         var pixelWidth = PixelWidth;
         var pixelHeight = PixelHeight;

         _renderTarget?.Dispose();
         _renderTarget = new RenderTargetImage(pixelWidth, pixelHeight, MSAALevel.None, PixelFormat);
         Handle = new IntPtr(_renderTarget.NativePointer);
         var args = new RenderTargetEventArgs(Handle, pixelWidth, pixelHeight, PixelFormat);
         args.RoutedEvent = RenderTargetCreatedOrUpdatedEvent;
         RaiseEvent(args);
      }
      catch (Exception e)
      {
         Log.Error(e.ToString());
         throw;
      }
   }

   protected override void OnInitialized()
   {
      base.OnInitialized();
      SizeChanged += OnSizeChanged;
      UpdateOrCreateRenderTarget();
   }
   
   private void OnSizeChanged(object sender, SizeChangedEventArgs e)
   {
        if (e.OriginalSource == this)
        {
            UpdateOrCreateRenderTarget();
        }
   }

   protected override void OnRender(DrawingContext context)
   {
      if (_renderTarget == null) return;
      
      if (sizeChanged)
      {
         context.BeginDraw(this);
         context.DrawImage(_renderTarget, Brushes.White, new Rect(Bounds.Size), new CornerRadius(0));
         context.EndDraw(this);
         sizeChanged = false;
      }
   }

   public void SaveCurrentFrame(Uri path, ImageFileType fileType)
   {
      _renderTarget.Save(path, fileType);
   }
}