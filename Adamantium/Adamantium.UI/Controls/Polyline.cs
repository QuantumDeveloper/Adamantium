using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
    public class Polyline : Shape
    {
        public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(Points),
            typeof(TrackingCollection<Vector2D>), typeof(Polygon),
            new PropertyMetadata(default(Vector2D),
                PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
                PropertyMetadataOptions.AffectsArrange | PropertyMetadataOptions.AffectsRender));
        
        public TrackingCollection<Vector2D> Points
        {
            get => GetValue<TrackingCollection<Vector2D>>(PointsProperty);
            set => SetValue(PointsProperty, value);
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
            context.DrawPolyline(Points, GetPen());
            context.EndDraw(this);
        }
    }
}