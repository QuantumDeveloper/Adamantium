using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public class Border : Decorator
   {
      public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
         typeof (Brush), typeof (Border),
         new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
         typeof (Thickness), typeof (Border),
         new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty BorderThicknessProperty =
         AdamantiumProperty.Register(nameof(BorderThickness),
            typeof (Thickness), typeof (Border),
            new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
         typeof (Brush), typeof (Border),
         new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

      public Brush BorderBrush
      {
         get { return GetValue<Brush>(BorderBrushProperty); }
         set { SetValue(BorderBrushProperty, value); }
      }

      public Brush Background
      {
         get { return GetValue<Brush>(BackgroundProperty); }
         set { SetValue(BackgroundProperty, value); }
      }

      public Thickness CornerRadius
      {
         get { return GetValue<Thickness>(CornerRadiusProperty); }
         set { SetValue(CornerRadiusProperty, value); }
      }

      public Thickness BorderThickness
      {
         get { return GetValue<Thickness>(BorderThicknessProperty); }
         set { SetValue(BorderThicknessProperty, value); }
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
         else
         {
            return new Size(padding.Left + padding.Right, padding.Bottom + padding.Top);
         }
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
         if (Child != null)
         {
            var padding = Padding + BorderThickness;
            Child.Arrange(new Rect(finalSize).Deflate(padding));
         }

         return finalSize;
      }

      protected override void OnRender(DrawingContext context)
      {
         var size = new Size(ActualWidth, ActualHeight);
         var borderThickness = BorderThickness;
         var cornerRadius = CornerRadius;
         base.OnRender(context);
         StreamGeometry geometry = new StreamGeometry();
         geometry.context.BeginFigure(new Point(cornerRadius.Left, 0));


         geometry.context.LineTo(new Point(size.Width - cornerRadius.Right, 0), borderThickness.Top);
         if (cornerRadius.Top > 0.0)
         {
            geometry.context.QuadraticBezier(new Point(size.Width - cornerRadius.Right, 0), new Point(size.Width, 0),
               new Point(size.Width, cornerRadius.Top), borderThickness.Right);
         }
         geometry.context.LineTo(new Point(size.Width, size.Height - cornerRadius.Right), BorderThickness.Right);
         if (cornerRadius.Right > 0.0)
         {
            geometry.context.QuadraticBezier(new Point(size.Width, size.Height - cornerRadius.Right), new Point(size),
               new Point(size.Width - cornerRadius.Right, size.Height), borderThickness.Right);
         }
         geometry.context.LineTo(new Point(cornerRadius.Bottom, size.Height), BorderThickness.Bottom);
         if (cornerRadius.Bottom > 0.0)
         {
            geometry.context.QuadraticBezier(new Point(cornerRadius.Bottom, size.Height), new Point(0, size.Height),
               new Point(0, size.Height - cornerRadius.Bottom), borderThickness.Bottom);
         }
         geometry.context.LineTo(new Point(0, cornerRadius.Left), borderThickness.Left);
         if (cornerRadius.Left > 0.0)
         {
            geometry.context.QuadraticBezier(new Point(0, cornerRadius.Left), new Point(0, 0),
               new Point(cornerRadius.Left, 0), borderThickness.Left);
         }



         geometry.VertexArray = geometry.context.VertexArray;
         geometry.IndicesArray = geometry.context.IndicesArray;

         context.BeginDraw(this);
         context.DrawGeometry(this, BorderBrush, null, geometry);
         context.DrawRectangle(this, Background, new Rect(new Point(BorderThickness.Left, BorderThickness.Top), RenderSize.Deflate(BorderThickness)), CornerRadius);
         context.EndDraw(this);
      }
   }
}
