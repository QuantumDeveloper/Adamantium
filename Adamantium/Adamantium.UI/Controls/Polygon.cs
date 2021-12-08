using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls
{
    public class Polygon : Shape
    {
        public Polygon()
        {
            geometry = new PolygonGeometry();
        }
        
        private PolygonGeometry geometry { get; set; }
        
        public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(Points),
            typeof(TrackingCollection<Vector2>), typeof(Polygon),
            new PropertyMetadata(null,
                PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure |
                PropertyMetadataOptions.AffectsArrange | PropertyMetadataOptions.AffectsRender, PointsChangedCallback));

        public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
            typeof(FillRule), typeof(Polygon),
            new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));

        private static void PointsChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        {
            if (a is Polygon polygon && e.NewValue != null)
            {
                polygon.geometry.Points = polygon.Points;
                if (e.OldValue is TrackingCollection<Vector2> oldCollection)
                {
                    polygon.UnsubscribeFromPointEvents(oldCollection);
                }

                polygon.SubscribeToPointEvents();
                polygon.InvalidateMeasure();
            }
        }

        private void UnsubscribeFromPointEvents(TrackingCollection<Vector2> collection)
        {
            collection.CollectionChanged -= PointsOnCollectionChanged;
        }

        private void SubscribeToPointEvents()
        {
            Points.CollectionChanged += PointsOnCollectionChanged;
        }

        private  void PointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateMeasure();
        }

        public TrackingCollection<Vector2> Points
        {
            get => GetValue<TrackingCollection<Vector2>>(PointsProperty);
            set => SetValue(PointsProperty, value);
        }
        
        public FillRule FillRule
        {
            get => GetValue<FillRule>(FillRuleProperty);
            set => SetValue(FillRuleProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Rect = new Rect(Vector2.Zero, geometry.Bounds.Size);
            return base.MeasureOverride(availableSize);
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            
            context.BeginDraw(this);
            geometry.FillRule = FillRule;
            context.DrawGeometry(Fill, geometry, GetPen());
            context.EndDraw(this);
        }
    }
}