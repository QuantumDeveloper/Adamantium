using System;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Controls
{
   public class Image:FrameworkComponent
   {
      public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
         typeof (Stretch), typeof (Image), new PropertyMetadata(Stretch.None, PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty SourceProperty = AdamantiumProperty.Register(nameof(Source),
         typeof (BitmapSource), typeof (Image), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty FilterBrushProperty = AdamantiumProperty.Register(nameof(FilterBrush),
         typeof(Brush), typeof(Image), new PropertyMetadata(Brushes.White, PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty RadiusXProperty = AdamantiumProperty.Register(nameof(RadiusX),
         typeof(Double), typeof(Image), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty RadiusYProperty = AdamantiumProperty.Register(nameof(RadiusY),
         typeof(Double), typeof(Image), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsRender));

      public Double RadiusX
      {
         get => GetValue<Double>(RadiusXProperty);
         set => SetValue(RadiusXProperty, value);
      }

      public Double RadiusY
      {
         get => GetValue<Double>(RadiusYProperty);
         set => SetValue(RadiusYProperty, value);
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
            context.DrawImage(this, source, FilterBrush, new Rect(Bounds.Size), RadiusX, RadiusY);
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
}
