using System;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Controls;

public class Image:MeasurableUIComponent
{
   public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
      typeof (Stretch), typeof (Image), new PropertyMetadata(Stretch.None, PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty SourceProperty = AdamantiumProperty.Register(nameof(Source),
      typeof (BitmapSource), typeof (Image), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty FilterBrushProperty = AdamantiumProperty.Register(nameof(FilterBrush),
      typeof(Brush), typeof(Image), new PropertyMetadata(Brushes.White, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
      typeof(CornerRadius), typeof(Image), new PropertyMetadata(new CornerRadius(0), PropertyMetadataOptions.AffectsRender));

   public CornerRadius CornerRadius
   {
      get => GetValue<CornerRadius>(CornerRadiusProperty);
      set => SetValue(CornerRadiusProperty, value);
   }

   public Brush FilterBrush {
      get => GetValue<Brush>(FilterBrushProperty);
      set => SetValue(FilterBrushProperty, value);
   }


   public Stretch Stretch
   {
      get => GetValue<Stretch>(StretchProperty);
      set => SetValue(StretchProperty, value);
   }

   public BitmapSource Source
   {
      get => GetValue<BitmapSource>(SourceProperty);
      set => SetValue(SourceProperty, value);
   }

   public Image()
   { }

   protected override Size MeasureOverride(Size availableSize)
   {
      if (Source != null)
      {
         var source = Source;

         var measuredSize = CalculateScaling(Stretch, availableSize, new Size(source.PixelWidth, source.PixelHeight));
         return measuredSize;
      }
      else
      {
         return new Size();
      }
   }

   protected override void OnRender(DrawingContext context)
   {
      base.OnRender(context);
      var source = Source;
      if (source != null)
      {
         context.BeginDraw(this);
         context.DrawImage(source, FilterBrush, new Rect(Bounds.Size), CornerRadius);
         context.BeginDraw(this);
      }
   }

   public Size CalculateScaling(Stretch stretch, Size destinationSize, Size sourceSize)
   {
      double sizeX = sourceSize.Width;
      double sizeY = sourceSize.Height;

      if (stretch != Stretch.None)
      {
         switch (stretch)
         {
            case Stretch.Uniform:
               sizeX = sizeY = Math.Min(destinationSize.Width, destinationSize.Height);
               break;
            case Stretch.UniformToFill:
               sizeX = sizeY = Math.Max(destinationSize.Width, destinationSize.Height);
               break;
            case Stretch.Fill:
               sizeX = destinationSize.Width;
               sizeY = destinationSize.Height;
               break;
         }
      }

      return new Size(sizeX, sizeY);
   }
}