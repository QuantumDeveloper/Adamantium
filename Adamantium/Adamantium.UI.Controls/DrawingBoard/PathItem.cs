using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A CONTOUR on the plane - what an imported drawing is made of: several sub-paths, filled by a rule, with
/// curves and arcs that no run of points can stand in for.
/// <para>It keeps the path DATA it was given and hands it to <see cref="SVGParser"/>, the engine's one reader of path
/// data - so what the plane shows is what the file says, and what is written back is the same grammar read by the same
/// code. Walked into a list of points instead, an icon arrived as one long stroke: no fill, no holes, and a line
/// joining the end of every sub-path to the start of the next.</para>
/// <para>Moving and resizing do not touch the contour. The item holds the box it stands in, and the contour is mapped
/// into it when it is drawn - so a path dragged across the plane and back is the path that arrived.</para></summary>
public class PathItem : ICanvasItem, ICanvasTransformed
{
    private string _data;
    private StreamGeometry _geometry;
    private Rect _source;

    public PathItem(string data, Brush fill, Brush stroke, double thickness = 1)
    {
        Data = data;
        Fill = fill;
        Stroke = stroke;
        Thickness = Math.Max(0, thickness);
        World = _source;
    }

    /// <summary>The path data this was made from - SVG's own grammar, kept as it came.</summary>
    public string Data
    {
        get => _data;
        set
        {
            _data = value;
            _geometry = new SVGParser().Parse(_data ?? string.Empty);
            _geometry.RecalculateBounds();
            _source = _geometry.Bounds;
        }
    }

    /// <summary>How a filled contour decides what is inside it - what makes the hole in a ring a hole.</summary>
    public FillRule FillRule
    {
        get => _geometry?.FillRule ?? FillRule.EvenOdd;
        set
        {
            if (_geometry != null) _geometry.FillRule = value;
        }
    }

    public Brush Fill { get; set; }

    public Brush Stroke { get; set; }

    public double Thickness { get; set; }

    /// <summary>Where it stands and how big, in world units. The contour is mapped into this box when it is drawn.
    /// </summary>
    public Rect World { get; set; }

    public CanvasTransform Transform { get; set; } = CanvasTransform.None;

    public int Order { get; set; }

    public string Sort => nameof(PathItem);

    public string Title => "Path";

    public Rect Bounds => World;

    public Double X
    {
        get => World.X;
        set => World = new Rect(value, World.Y, World.Width, World.Height);
    }

    public Double Y
    {
        get => World.Y;
        set => World = new Rect(World.X, value, World.Width, World.Height);
    }

    public Double Width
    {
        get => World.Width;
        set => World = new Rect(World.X, World.Y, Math.Max(1e-9, value), World.Height);
    }

    public Double Height
    {
        get => World.Height;
        set => World = new Rect(World.X, World.Y, World.Width, Math.Max(1e-9, value));
    }

    public Color? Paint => (Fill as SolidColorBrush)?.Color ?? (Stroke as SolidColorBrush)?.Color;

    public void PaintWith(Color color)
    {
        if (Fill != null) Fill = new SolidColorBrush(color);
        else Stroke = new SolidColorBrush(color);
    }

    public ICanvasItem Copy() =>
        new PathItem(Data, Fill, Stroke, Thickness) { World = World, Transform = Transform, FillRule = FillRule };

    public void Move(Vector2 worldDelta) =>
        World = new Rect(World.X + worldDelta.X, World.Y + worldDelta.Y, World.Width, World.Height);

    public void Resize(Rect world) =>
        World = new Rect(world.X, world.Y, Math.Max(1e-9, world.Width), Math.Max(1e-9, world.Height));

    /// <summary>Hit when the point is in its box. The contour itself would be the honest answer, and a filled one can
    /// only say so by being tessellated - which is a mesh per question. An imported drawing is picked from a list or
    /// by a band round it far more often than by a press inside one of its holes.</summary>
    public bool HitTest(Vector2 world, double tolerance) => World.Inflate(tolerance).Contains(world);

    public void Render(IDrawingSession session, InfiniteCanvas canvas)
    {
        if (_geometry == null || canvas == null) return;

        var box = canvas.ToScreen(World);

        if (box.Width <= 0 || box.Height <= 0) return;

        var pen = Stroke != null && Thickness > 0
            ? new Pen(Stroke, Thickness * canvas.Scale)
            : null;

        session.DrawGeometry(Fill, _geometry, pen, Place(box));
    }

    // The contour states itself in the file's own units; this is what puts it in the box the item stands in, on screen.
    private Matrix4x4F Place(Rect box)
    {
        var sx = _source.Width > 1e-9 ? box.Width / _source.Width : 1;
        var sy = _source.Height > 1e-9 ? box.Height / _source.Height : 1;

        var into = Matrix4x4F.Translation((float)-_source.X, (float)-_source.Y, 0)
                   * Matrix4x4F.Scaling((float)sx, (float)sy, 1)
                   * Matrix4x4F.Translation((float)box.X, (float)box.Y, 0);

        if (Transform.Angle == 0 && Transform.SkewX == 0 && Transform.SkewY == 0) return into;

        return into * Transform.Matrix(new Vector2(box.X + box.Width / 2, box.Y + box.Height / 2));
    }
}
