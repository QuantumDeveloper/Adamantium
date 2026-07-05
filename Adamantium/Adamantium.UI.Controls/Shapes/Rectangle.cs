using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

public class Rectangle : Shape
{
   static Rectangle()
   {
      // Box-shape default (as in WPF): fill the layout slot. Path/Polygon/Line keep the base Stretch.None.
      StretchProperty.OverrideMetadata(typeof(Rectangle), new PropertyMetadata(Stretch.Fill,
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));
   }

   public Rectangle()
   {
   }


   public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
      typeof(CornerRadius), typeof(Rectangle),
      new PropertyMetadata(new CornerRadius(0),
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender));

   public CornerRadius CornerRadius
   {
      get => GetValue<CornerRadius>(CornerRadiusProperty);
      set => SetValue(CornerRadiusProperty, value);
   }

   protected override void OnRender(IDrawingContext context)
   {
      base.OnRender(context);

      var dstRect = Rect.Deflate(StrokeThickness / 2);
      context.ForControl(this).DrawRectangle(Fill, dstRect, CornerRadius, GetPen());
   }

}