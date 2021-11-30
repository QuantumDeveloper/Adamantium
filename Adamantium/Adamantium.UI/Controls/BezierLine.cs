using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
    public class BezierLine : Line
    {
        public static readonly AdamantiumProperty BezierTypeProperty = AdamantiumProperty.Register(nameof(BezierType),
            typeof(BezierLine), typeof(Shape),
            new PropertyMetadata(BezierType.Quadratic,
                PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender));
        
        public static readonly AdamantiumProperty ControlPoint1Property = AdamantiumProperty.Register(nameof(ControlPoint1),
            typeof(BezierLine), typeof(Point),
            new PropertyMetadata(default(Point), PropertyMetadataOptions.AffectsRender));
        
        public static readonly AdamantiumProperty ControlPoint2Property = AdamantiumProperty.Register(nameof(ControlPoint2),
            typeof(BezierLine), typeof(Point),
            new PropertyMetadata(default(Point), PropertyMetadataOptions.AffectsRender));

        public BezierType BezierType
        {
            get => GetValue<BezierType>(BezierTypeProperty);
            set => SetValue(BezierTypeProperty, value);
        }
        
        public Point ControlPoint1
        {
            get => GetValue<Point>(ControlPoint1Property);
            set => SetValue(ControlPoint1Property, value);
        }
        
        public Point ControlPoint2
        {
            get => GetValue<Point>(ControlPoint2Property);
            set => SetValue(ControlPoint2Property, value);
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            var start = new Point(X1, Y1);
            var end = new Point(X2, Y2);
         
            context.BeginDraw(this);
            //context.DrawLine();
            context.EndDraw(this);
        }
    }
}