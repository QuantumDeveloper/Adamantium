using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.RoutedEvents;
using Serilog;
using Serilog.Core;

namespace Adamantium.UI.Controls;

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
      new PropertyMetadata(IntPtr.Zero, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty PixelWidthProperty =
      AdamantiumProperty.RegisterReadOnly(nameof(PixelWidth), typeof(Int32), typeof(RenderTargetPanel),
         new PropertyMetadata(0));

   public static readonly AdamantiumProperty PixelHeightProperty =
      AdamantiumProperty.RegisterReadOnly(nameof(PixelHeight), typeof(Int32), typeof(RenderTargetPanel),
         new PropertyMetadata(0));

   public static readonly AdamantiumProperty CanPresentProperty =
      AdamantiumProperty.RegisterReadOnly(nameof(CanPresent), typeof(bool), typeof(RenderTargetPanel),
         new PropertyMetadata(false, OnCanPresentChanged));
   
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

   public bool CanPresent
   {
      get => GetValue<bool>(CanPresentProperty);
      set => SetValue(CanPresentProperty, value);
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

   private static void RenderTargetParametersChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
   {
      var image = adamantiumObject as RenderTargetPanel;
      image?.UpdateOrCreateRenderTarget();
   }

   protected override void OnSizeChanged(SizeChangedEventArgs e)
   {
      Log.Logger.Debug($"Size changed event. New size: {e.NewSize}");
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
         
         
         CanPresent = false;
         var wnd = GetWindow();
         _nextRenderTarget = new RenderTargetImage(wnd.GetDrawingContext(), PixelWidth, PixelHeight, MSAALevel.None, SurfaceFormat);
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
      SizeChanged += OnSizeChanged;
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
      context.DrawRectangle(Background, new Rect(new Size(ActualWidth, ActualHeight)));
      if (CanPresent)
      {
         _currentRenderTarget?.Dispose();
         _currentRenderTarget = _nextRenderTarget;
         unsafe
         {
            Handle = new IntPtr(_currentRenderTarget.NativePointer);
         }
         context.AddImage(_currentRenderTarget);
      }
      else
      {
         context.AddImage(_currentRenderTarget);
      }
   }


   public async void SaveCurrentFrame(Uri path, ImageFileType fileType)
   {
      await Task.Run(() => _currentRenderTarget.Save(path, fileType));
   }
}