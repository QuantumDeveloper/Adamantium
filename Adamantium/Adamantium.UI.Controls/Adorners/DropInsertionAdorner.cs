using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// The drag-drop INSERTION CARET: a thin horizontal line across the adorned items host with a triangular arrowhead at each
/// end pointing inward (the classic list insertion mark), drawn at the Y an item would land - so a drop shows exactly
/// WHERE it will go. The drag engine recreates it as the cursor moves (a fresh adorner renders immediately).
/// </summary>
public class DropInsertionAdorner : Adorner
{
    private readonly double _lineY;

    public DropInsertionAdorner(UIComponent host, double lineY) : base(host)
    {
        _lineY = lineY;
    }

    // Deliberately NOT the accent colour - the selection highlight is the accent, so an accent caret blends into a
    // selected item. Public so a caller / theme can override it.
    public Brush Stroke { get; set; } = new SolidColorBrush(Colors.Orange);
    public double Thickness { get; set; } = 2.5;
    public double CapWidth { get; set; } = 7.0;
    public double CapHeight { get; set; } = 6.0;

    protected override void OnRender(IDrawingContext context)
    {
        var width = AdornedBounds.Width;
        var y = _lineY;
        var cw = CapWidth;
        var ch = CapHeight;
        var session = context.ForControl(this);

        // The bar, inset by the caret width so the line meets the arrowheads.
        session.DrawRectangle(Stroke, new Rect(cw, y - Thickness / 2.0, Math.Max(0, width - 2 * cw), Thickness), null);

        // Two filled triangular arrowheads pointing inward: left ▶ at the start, right ◀ at the end.
        var geometry = new StreamGeometry();
        var figures = geometry.Open();
        figures.BeginFigure(new Vector2(0, (float)(y - ch)), true, true)
            .PolylineLineTo([new Vector2((float)cw, (float)y), new Vector2(0, (float)(y + ch))], true);
        figures.BeginFigure(new Vector2((float)width, (float)(y - ch)), true, true)
            .PolylineLineTo([new Vector2((float)(width - cw), (float)y), new Vector2((float)width, (float)(y + ch))], true);
        session.DrawGeometry(Stroke, geometry, null);
    }
}
