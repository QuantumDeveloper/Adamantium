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
            typeof(BezierLine), typeof(Vector2D),
            new PropertyMetadata(default(Vector2D), PropertyMetadataOptions.AffectsRender));
        
        public static readonly AdamantiumProperty ControlPoint2Property = AdamantiumProperty.Register(nameof(ControlPoint2),
            typeof(BezierLine), typeof(Vector2D),
            new PropertyMetadata(default(Vector2D), PropertyMetadataOptions.AffectsRender));

        public BezierType BezierType
        {
            get => GetValue<BezierType>(BezierTypeProperty);
            set => SetValue(BezierTypeProperty, value);
        }
        
        public Vector2D ControlPoint1
        {
            get => GetValue<Vector2D>(ControlPoint1Property);
            set => SetValue(ControlPoint1Property, value);
        }
        
        public Vector2D ControlPoint2
        {
            get => GetValue<Vector2D>(ControlPoint2Property);
            set => SetValue(ControlPoint2Property, value);
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            var start = new Vector2D(X1, Y1);
            var end = new Vector2D(X2, Y2);
         
            context.BeginDraw(this);
            //context.DrawLine();
            context.EndDraw(this);
        }
    }
}