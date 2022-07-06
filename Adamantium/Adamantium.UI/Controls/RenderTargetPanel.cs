using System;
using Adamantium.Engine.Graphics;
using Adamantium.Imaging;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.RoutedEvents;
using Adamantium.Win32;

namespace Adamantium.UI.Controls;

public class RenderTargetPanel: Grid
{
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
      
   public Int32 PixelWidth
   {
      get => GetValue<Int32>(PixelWidthProperty);
      set => SetValue(PixelWidthProperty, value);
   }

   public Int32 PixelHeight
   {
      get => GetValue<Int32>(PixelHeightProperty);
      set => SetValue(PixelHeightProperty, value);
   }

   private static void RenderTargetParametersChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
   {
      var image = adamantiumObject as RenderTargetPanel;
      image?.UpdateOrCreateRenderTarget();
   }

   private RenderTargetImage rendertarget;

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
         if ((int)ActualWidth > 0 && (int)ActualHeight > 0)
         {
            PixelWidth = (int)ActualWidth;
            PixelHeight = (int) ActualHeight;
            var pixelWidth = PixelWidth;
            var pixelHeight = PixelHeight;

            rendertarget?.Dispose();
            //rendertarget = new RenderTargetImage(pixelWidth, pixelHeight, PixelFormat, MSAALevel.None, 1, TextureFlags.ShaderResource, ResourceUsage.Default, ResourceOptionFlags.Shared);
            //Debug.WriteLine("RenderTargetPanel size changed = "+ pixelWidth + " "+ pixelHeight);
            //Handle = rendertarget.NativePointer;
            //if (!isInitialized)
            //{
            //   RenderTargetInitialized?.Invoke(this, new RenderTargetEventArgs(rendertarget.NativePointer, pixelWidth, pixelHeight, PixelFormat));
            //   isInitialized = true;
            //}
            //else
            //{
            //   RenderTargetChanged?.Invoke(this, new RenderTargetEventArgs(rendertarget.NativePointer, pixelWidth, pixelHeight, PixelFormat));
            //}
         }
      }
      catch (Exception e)
      {
         MessageBox.Show(e.Message + e.StackTrace);
      }
   }

   private bool isInitialized;

   public event EventHandler<RenderTargetEventArgs> OnRenderEvent;

   public event EventHandler<RenderTargetEventArgs> RenderTargetChanged;

   public event EventHandler<RenderTargetEventArgs> RenderTargetInitialized;

   protected override void OnRender(DrawingContext context)
   {
      if (rendertarget != null)
      {
         OnRenderEvent?.Invoke(this, new RenderTargetEventArgs(rendertarget.NativePointer, PixelWidth, PixelHeight, PixelFormat));
         if (sizeChanged)
         {
            context.BeginDraw(this);
            context.DrawImage(rendertarget, Brushes.White, new Rect(Bounds.Size), new CornerRadius(0));
            context.EndDraw(this);
            sizeChanged = false;
         }
      }
   }

   public void SaveCurrentFrame(Uri path, ImageFileType fileType)
   {
      rendertarget.Save(path, fileType);
   }
}

public class RenderTargetEventArgs : EventArgs
{
   public IntPtr Handle { get; }
   public Int32 Width { get; }
   public Int32 Height { get; }
   public SurfaceFormat PixelFormat { get; }

   public RenderTargetEventArgs(IntPtr pointer, Int32 width, Int32 height, SurfaceFormat format)
   {
      Handle = pointer;
      Width = width;
      Height = height;
      PixelFormat = format;
   }
}