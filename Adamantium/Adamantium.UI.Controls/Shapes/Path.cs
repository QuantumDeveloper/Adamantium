using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Shapes;

public class Path : Shape
{
    public static readonly AdamantiumProperty DataProperty =
        AdamantiumProperty.Register(nameof(Data), typeof(Geometry), typeof(Path),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, DataChangedCallback));

    private static void DataChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is Path path)
        {
            if (e.OldValue is Geometry geometry1) geometry1.ComponentUpdated -= path.OnGeometryUpdated;
            if (e.NewValue is Geometry geometry2) geometry2.ComponentUpdated += path.OnGeometryUpdated;
        }
    }

    private void OnGeometryUpdated(object sender, ComponentUpdatedEventArgs e)
    {
        InvalidateMeasure();
    }

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
            Data.RecalculateBounds();

            Rect = Data.Bounds;
        }
        return base.MeasureOverride(Rect.Size);
    }

    protected override void OnRender(IDrawingContext context)
    {
        context.ForControl(this).DrawGeometry(Fill, Data, GetPen());
    }
}