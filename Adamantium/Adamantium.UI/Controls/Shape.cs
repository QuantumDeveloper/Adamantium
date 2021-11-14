using System;
using Adamantium.Core.Collections;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public abstract class Shape : FrameworkComponent
   {
      protected Rect Rect;
      
      protected Shape()
      {
         BoundingRectangle = new Rect();
      }

      public static readonly AdamantiumProperty FillProperty = AdamantiumProperty.Register(nameof(Fill),
         typeof (Brush), typeof (Shape),
         new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.BindsTwoWayByDefault|PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty StrokeProperty = AdamantiumProperty.Register(nameof(Stroke),
         typeof (Brush), typeof (Shape),
         new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.BindsTwoWayByDefault));

      public static readonly AdamantiumProperty StrokeThicknessProperty =
         AdamantiumProperty.Register(nameof(StrokeThickness),
            typeof (Double), typeof (Shape),
            new PropertyMetadata((Double) 0,
               PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender, CoerceStrokeThickness ));

      public static readonly AdamantiumProperty StrokeDashArrayProperty =
         AdamantiumProperty.Register(nameof(StrokeDashArray),
            typeof (AdamantiumCollection<Double>), typeof (Shape), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
         typeof (Stretch), typeof (Shape),
         new PropertyMetadata(Stretch.None,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty StrokeDashCapProperty =
         AdamantiumProperty.Register(nameof(StrokeDashCap), typeof (PenLineCap), typeof (Shape),
            new PropertyMetadata(PenLineCap.Flat, PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty StartLineCapProperty =
         AdamantiumProperty.Register(nameof(StartLineCap), typeof(PenLineCap), typeof(Shape),
            new PropertyMetadata(PenLineCap.Flat, PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty EndLineCapProperty =
         AdamantiumProperty.Register(nameof(EndLineCap), typeof(PenLineCap), typeof(Shape),
            new PropertyMetadata(PenLineCap.Flat, PropertyMetadataOptions.AffectsRender));

      public Brush Fill
      {
         get => GetValue<Brush>(FillProperty);
         set => SetValue(FillProperty, value);
      }

      public Brush Stroke
      {
         get => GetValue<Brush>(StrokeProperty);
         set => SetValue(StrokeProperty, value);
      }

      public Double StrokeThickness
      {
         get => GetValue<Double>(StrokeThicknessProperty);
         set => SetValue(StrokeThicknessProperty, value);
      }

      public AdamantiumCollection<Double> StrokeDashArray
      {
         get => GetValue<AdamantiumCollection<Double>>(StrokeDashArrayProperty);
         set => SetValue(StrokeDashArrayProperty, value);
      }

      public Stretch Stretch
      {
         get => GetValue<Stretch>(StretchProperty);
         set => SetValue(StretchProperty, value);
      }

      public PenLineCap StrokeDashCap
      {
         get => GetValue<PenLineCap>(StrokeDashCapProperty);
         set => SetValue(StrokeDashCapProperty, value);
      }

      public PenLineCap StartLineCap { get; set; }

      public PenLineCap EndLineCap { get; set; }

      protected Rect BoundingRectangle { get; set; }

      private static object CoerceStrokeThickness(AdamantiumComponent adamantiumObject, object baseValue)
      {
         Double value = (Double) baseValue;
         if (value < 0)
         {
            return (Double) 0;
         }
         return baseValue;
      }

      protected override Size MeasureOverride(Size availableSize)
      {
         Size shapeSize = BoundingRectangle.Size;
         Size desiredSize = new Size(availableSize.Width, availableSize.Height);

         if (double.IsInfinity(availableSize.Width))
         {
            desiredSize.Width = shapeSize.Width;
         }

         if (double.IsInfinity(availableSize.Height))
         {
            desiredSize.Height = shapeSize.Height;
         }
         
         if (double.IsNaN(Width) && HorizontalAlignment != HorizontalAlignment.Stretch)
         {
            desiredSize.Width = shapeSize.Width;
         }

         if (double.IsNaN(Height) && VerticalAlignment != VerticalAlignment.Stretch)
         {
            desiredSize.Height = shapeSize.Height;
         }

         if (!double.IsNaN(Width))
         {
            desiredSize.Width = Width;
         }

         if (!double.IsNaN(Height))
         {
            desiredSize.Height = Height;
         }

         switch (Stretch)
         {
            case Stretch.Uniform:
               shapeSize.Width = Math.Min(desiredSize.Width, desiredSize.Height);
               shapeSize.Height = Math.Min(desiredSize.Width, desiredSize.Height);
               break;
            case Stretch.UniformToFill:
               double maxValue = Math.Max(desiredSize.Width, desiredSize.Height);
               shapeSize.Width = maxValue;
               shapeSize.Height = maxValue;
               break;
            case Stretch.Fill:
               shapeSize.Width = Math.Max(desiredSize.Width, desiredSize.Height);
               shapeSize.Height = Math.Max(desiredSize.Width, desiredSize.Height);
               break;
            default:
               shapeSize = desiredSize;
               break;
         }
         Rect = new Rect(shapeSize);
         return new Size(shapeSize.Width, shapeSize.Height);
      }
   }
}
