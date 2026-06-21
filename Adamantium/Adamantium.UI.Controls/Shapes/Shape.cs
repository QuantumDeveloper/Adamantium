using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

public abstract class Shape : InputUIComponent
{
   protected Rect Rect;
     
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
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender, CoerceStrokeThickness ));

   public static readonly AdamantiumProperty StrokeDashArrayProperty =
      AdamantiumProperty.Register(nameof(StrokeDashArray),
         typeof (TrackingCollection<Double>), typeof(Shape), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
      typeof (Stretch), typeof (Shape),
      new PropertyMetadata(Stretch.None,
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StartLineCapProperty =
      AdamantiumProperty.Register(nameof(StartLineCap), typeof(PenLineCap), typeof(Shape),
         new PropertyMetadata(PenLineCap.Flat, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty EndLineCapProperty =
      AdamantiumProperty.Register(nameof(EndLineCap), typeof(PenLineCap), typeof(Shape),
         new PropertyMetadata(PenLineCap.Flat, PropertyMetadataOptions.AffectsRender));
      
   public static readonly AdamantiumProperty StrokeLineJoinProperty =
      AdamantiumProperty.Register(nameof(StrokeLineJoin), typeof(PenLineJoin), typeof(Shape),
         new PropertyMetadata(PenLineJoin.Miter, PropertyMetadataOptions.AffectsRender));
      
   public static readonly AdamantiumProperty StrokeDashOffsetProperty =
      AdamantiumProperty.Register(nameof(StrokeDashOffset), typeof(Double), typeof(Shape),
         new PropertyMetadata(0d, PropertyMetadataOptions.AffectsRender));

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

   public TrackingCollection<Double> StrokeDashArray
   {
      get => GetValue<TrackingCollection<Double>>(StrokeDashArrayProperty);
      set => SetValue(StrokeDashArrayProperty, value);
   }

   public Stretch Stretch
   {
      get => GetValue<Stretch>(StretchProperty);
      set => SetValue(StretchProperty, value);
   }

   public PenLineCap StartLineCap
   {
      get => GetValue<PenLineCap>(StartLineCapProperty);
      set => SetValue(StartLineCapProperty, value);
   }

   public PenLineCap EndLineCap
   {
      get => GetValue<PenLineCap>(EndLineCapProperty);
      set => SetValue(EndLineCapProperty, value);
   }

   public PenLineJoin StrokeLineJoin
   {
      get => GetValue<PenLineJoin>(StrokeLineJoinProperty);
      set => SetValue(StrokeLineJoinProperty, value);
   }

   public Double StrokeDashOffset
   {
      get => GetValue<Double>(StrokeDashOffsetProperty);
      set => SetValue(StrokeDashOffsetProperty, value);
   }
      
   private static object CoerceStrokeThickness(AdamantiumComponent adamantiumAdamantiumComponent, object baseValue)
   {
      Double value = (Double) baseValue;
      if (value < 0)
      {
         return (Double) 0;
      }
      return baseValue;
   }

   public Pen GetPen()
   {
      return new Pen(
         Stroke,
         StrokeThickness,
         StrokeDashOffset,
         StrokeDashArray,
         StartLineCap,
         EndLineCap,
         StrokeLineJoin);
   }

   protected override Size MeasureOverride(Size availableSize)
   {
      Size shapeSize = Rect.Size;

      // The shape's natural (intrinsic) size, captured before Rect is recomputed below. This is what the shape reports
      // to layout: a Stretch-aligned shape with no explicit size desires its intrinsic size (0 for a box shape like
      // Rectangle/Ellipse), NOT the available space - stretching fills the box via Rect (render), at arrange/render time,
      // not by inflating the desired size. (WPF behaviour; previously a Stretch shape desired the whole available area.)
      double naturalWidth = Rect.Width + Rect.X;
      double naturalHeight = Rect.Height + Rect.Y;

      Size desiredSize = new Size(availableSize.Width, availableSize.Height).Deflate(new Thickness(StrokeThickness/2));

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
         desiredSize.Width = shapeSize.Width + Rect.X;
      }

      if (double.IsNaN(Height) && VerticalAlignment != VerticalAlignment.Stretch)
      {
         desiredSize.Height = shapeSize.Height + Rect.Y;
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

      // Report the intrinsic size to layout (explicit Width/Height win); Rect above still fills for rendering.
      return new Size(
         double.IsNaN(Width) ? naturalWidth : Width,
         double.IsNaN(Height) ? naturalHeight : Height);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      return Rect.Size;
   }
}