using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
    public class Polygon : Shape
    {
        public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(Points),
            typeof(TrackingCollection<Vector2D>), typeof(Polygon),
            new PropertyMetadata(null,
                PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
                PropertyMetadataOptions.AffectsArrange | PropertyMetadataOptions.AffectsRender));

        public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
            typeof(FillRule), typeof(Polygon),
            new PropertyMetadata(Mathematics.FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));

        public TrackingCollection<Vector2D> Points
        {
            get => GetValue<TrackingCollection<Vector2D>>(PointsProperty);
            set => SetValue(PointsProperty, value);
        }
        
        public FillRule FillRule
        {
            get => GetValue<FillRule>(FillRuleProperty);
            set => SetValue(FillRuleProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var maxX = Points.Select(x=>x.X).Max();
            var maxY = Points.Select(y=>y.Y).Max();
            BoundingRectangle = new Rect(new Vector2D(0), new Vector2D(maxX, maxY));
            return base.MeasureOverride(availableSize);
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            
            context.BeginDraw(this);
            Geometry geometry = new PolygonGeometry(Points, FillRule); 
            context.DrawGeometry(Fill, geometry, GetPen());
            context.EndDraw(this);
        }
    }
}