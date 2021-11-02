using System;
using Adamantium.Core.Collections;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public abstract class Shape : FrameworkComponent
   {
      protected Shape()
      {
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
               PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender, PropertyChangedCallback, CoerceStrokeThickness ));

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

      public virtual Geometry RenderGeometry { get; private set; }


      private static void PropertyChangedCallback(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs adamantiumPropertyChangedEventArgs)
      {

      }

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
         Rect shapeBounds = RenderGeometry.Bounds;
         Size shapeSize = new Size(shapeBounds.Right, shapeBounds.Bottom);
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
            desiredSize.Width = 0;
         }
         

         if (double.IsNaN(Height) && VerticalAlignment != VerticalAlignment.Stretch)
         {
            desiredSize.Height = 0;
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
               shapeSize = new Size(desiredSize.Width, desiredSize.Height);
               break;
         }
         Rect = new Rect(shapeSize);
         return new Size(shapeSize.Width, shapeSize.Height);
      }

      protected Rect Rect;
   }
}
