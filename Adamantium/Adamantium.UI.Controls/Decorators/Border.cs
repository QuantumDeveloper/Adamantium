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
      // Fill the arranged slot and lay the child out inside it (minus border+padding); the child positions itself within
      // via its own alignment. Returning the CHILD's size instead collapsed a stretched border to a non-stretch child -
      // e.g. a Button's chrome shrank to its centred ContentPresenter and the text spilled outside. Shrink-to-content is
      // a MEASURE concern (MeasureOverride already returns child+padding, honouring Width/Height), not arrange.
      var padding = Padding + BorderThickness;
      Child?.Arrange(new Rect(finalSize).Deflate(padding));
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

      var ctx = context.ForControl(this);

      var hasThickness = borderThickness.Left != 0 || borderThickness.Top != 0 || borderThickness.Right != 0 || borderThickness.Bottom != 0;

      // UNIFORM border (equal thickness on all sides + equal corner radii): draw the fill AND the border as ONE rounded
      // rect with a pen, so it goes to the SDF item-background batch - reconstructed analytically and self-anti-aliased in
      // one instanced draw, exactly like a plain solid fill. The old separate CombinedGeometry ring went through the per-
      // unit analytic-AA fringe, whose outward-offset ring OVER-BLENDED its own coincident inner/outer edges (a 1px ring is
      // as thin as the fringe) and hatched the corners - visible on a translucent card (the menu). The batch's SDF stroke
      // is CENTRE-aligned, so drawing the pen on the rect inset by HALF the thickness lands the stroke in the outer
      // `thickness` px == a WPF inside border, with the fill composited under it in the same pass.
      if (borderThickness.IsUniform && cornerRadius.IsUniform && hasThickness && BorderBrush.IsVisible())
      {
         var t = borderThickness.Left;
         var half = t * 0.5;
         var penRect = outerRect.Deflate(new Thickness(half));
         var penRadius = new CornerRadius(Math.Max(0.0, cornerRadius.TopLeft - half));
         var fill = Background.IsVisible() ? Background : Brushes.Transparent;
         ctx.DrawRectangle(fill, penRect, penRadius, new Pen(BorderBrush, t));
         return;
      }

      // Fallback (no border, or a NON-uniform border/corner the SDF rect model can't express): the solid fill still SDF-
      // batches; the ring goes through the per-unit path. Only record draws that produce visible pixels - a transparent
      // brush would otherwise still build a fill unit + fringe every frame (the ListBox FPS drop); hit-testing is
      // bounds-based, so nothing depends on the invisible draw.
      if (Background.IsVisible())
         ctx.DrawRectangle(Background, innerRect, innerRadius);

      if (hasThickness && BorderBrush.IsVisible())
      {
         var combined = new CombinedGeometry
         {
            GeometryCombineMode = GeometryCombineMode.Exclude,
            Geometry1 = new RectangleGeometry(outerRect, cornerRadius),
            Geometry2 = new RectangleGeometry(innerRect, innerRadius)
         };
         ctx.DrawGeometry(BorderBrush, combined);
      }
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