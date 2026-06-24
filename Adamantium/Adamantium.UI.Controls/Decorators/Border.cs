using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Decorators;

public class Border : Decorator
{
   public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
      typeof (Brush), typeof (Border),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
      typeof (CornerRadius), typeof (Border),
      new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty BorderThicknessProperty =
      AdamantiumProperty.Register(nameof(BorderThickness),
         typeof (Thickness), typeof (Border),
         new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
      typeof (Brush), typeof (Border),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public Brush BorderBrush
   {
      get => GetValue<Brush>(BorderBrushProperty);
      set => SetValue(BorderBrushProperty, value);
   }

   public Brush Background
   {
      get => GetValue<Brush>(BackgroundProperty);
      set => SetValue(BackgroundProperty, value);
   }

   public CornerRadius CornerRadius
   {
      get => GetValue<CornerRadius>(CornerRadiusProperty);
      set => SetValue(CornerRadiusProperty, value);
   }

   public Thickness BorderThickness
   {
      get => GetValue<Thickness>(BorderThicknessProperty);
      set => SetValue(BorderThicknessProperty, value);
   }

   public Border()
   {
   }

   protected override Size MeasureOverride(Size availableSize)
   {
      var child = Child;
      var padding = Padding + BorderThickness;
      var size = availableSize.Deflate(padding);
      if (child != null)
      {
         child.Measure(size);
         return child.DesiredSize.Inflate(padding);
      }

      return new Size(padding.Left + padding.Right, padding.Bottom + padding.Top);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      var padding = Padding + BorderThickness;
      Child?.Arrange(new Rect(finalSize).Deflate(padding));

      if (Child != null)
      {
         var result = Child.Bounds.Size.Inflate(padding);
         if (!Double.IsNaN(Width))
         {
            result.Width = Math.Max(result.Width, Width);
         }
            
         if (!Double.IsNaN(Height))
         {
            result.Height = Math.Max(result.Height, Height);
         }

         return result;
      }
      
      return finalSize;
   }

   protected override void OnRender(IDrawingContext context)
   {
      var borderThickness = BorderThickness;
      var cornerRadius = CornerRadius;
      base.OnRender(context);

      var outerRect = new Rect(new Size(ActualWidth, ActualHeight));
      // Inset by the FULL border thickness and shrink the corner radii concentrically, so the border ring keeps a
      // uniform width all the way around (including the rounded corners) instead of pinching at them.
      var innerRect = outerRect.Deflate(borderThickness);
      var innerRadius = DeflateCornerRadius(cornerRadius, borderThickness);

      var combined = new CombinedGeometry
      {
         GeometryCombineMode = GeometryCombineMode.Exclude,
         Geometry1 = new RectangleGeometry(outerRect, cornerRadius),
         Geometry2 = new RectangleGeometry(innerRect, innerRadius)
      };

      context.ForControl(this)
         .DrawRectangle(Background, innerRect, innerRadius)
         .DrawGeometry(BorderBrush, combined);
   }

   // Each corner shrinks by the thickness of the two edges meeting there (clamped at 0) so the inner curve stays
   // parallel to the outer one. Scalar (circular) corners can't be perfectly concentric under non-uniform thickness;
   // taking the larger adjacent edge keeps the inner arc from bulging past the border on the thicker side.
   private static CornerRadius DeflateCornerRadius(CornerRadius radius, Thickness border)
   {
      return new CornerRadius(
         Math.Max(0.0, radius.TopLeft - Math.Max(border.Left, border.Top)),
         Math.Max(0.0, radius.TopRight - Math.Max(border.Top, border.Right)),
         Math.Max(0.0, radius.BottomRight - Math.Max(border.Right, border.Bottom)),
         Math.Max(0.0, radius.BottomLeft - Math.Max(border.Bottom, border.Left)));
   }
}