using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
    public class Path : Shape
    {
        public static readonly AdamantiumProperty DataProperty =
            AdamantiumProperty.Register(nameof(Data), typeof(Geometry), typeof(Path));

        public Path()
        {
        }

        public Geometry Data
        {
            get => GetValue<Geometry>(DataProperty);
            set => SetValue(DataProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Data != null)
            {
                Rect = Data.Bounds;
            }
            return base.MeasureOverride(availableSize);
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            
            context.BeginDraw(this);
            context.DrawGeometry(Fill, Data, GetPen());
            context.EndDraw(this);
        }
    }
}