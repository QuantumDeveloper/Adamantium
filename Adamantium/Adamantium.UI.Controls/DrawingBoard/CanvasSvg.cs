using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Writes a DRAWING out as SVG and reads one back.
/// <para>SVG and not a format of our own because a drawing is the half of a canvas that other people's tools have a
/// claim on: it is opened in a browser, dropped into a document, edited in Inkscape and sent back. A graph is the
/// opposite - it means something only here - which is why it is kept as JSON by
/// <see cref="CanvasGraphSerializer"/>.</para>
/// <para>TWO READERS ARE SERVED AT ONCE. Everything is written as the standard element that says it - a stroke is a
/// polyline, a polygon is a polygon with its corners spelled out - so a stranger's viewer draws it correctly with no
/// knowledge of us. What SVG has no word for rides alongside in <c>data-</c> attributes: how many sides a polygon was
/// asked for, which kind of spline a curve is, the four separate corner radii of a rectangle. Ours reads those and
/// gets the object back exactly; anyone else ignores them and still sees the picture.</para>
/// <para>What is NOT written: controls placed on the plane and the wires between nodes. A control is a living thing
/// with a template and behaviour, and a rectangle labelled "Button" in a file would be a lie about what it is.</para>
/// </summary>
public static class CanvasSvg
{
    public const string Namespace = "http://www.w3.org/2000/svg";

    private static readonly XNamespace Svg = Namespace;

    /// <summary>Everything the canvas is drawing, in world units, in a document sized to hold it.</summary>
    public static string Save(InfiniteCanvas canvas)
    {
        if (canvas == null) return null;

        var items = new List<ICanvasItem>();

        foreach (var item in canvas.ItemsHere())
        {
            if (Drawable(item)) items.Add(item);
        }

        return Save(items);
    }

    /// <summary>The same, for a selection or any other list somebody has in hand.</summary>
    public static string Save(IReadOnlyList<ICanvasItem> items)
    {
        items ??= Array.Empty<ICanvasItem>();

        var box = Around(items);
        var root = new XElement(Svg + "svg",
            new XAttribute("xmlns", Namespace),
            new XAttribute("version", "1.1"),
            new XAttribute("width", Num(box.Width)),
            new XAttribute("height", Num(box.Height)),
            new XAttribute("viewBox", $"{Num(box.X)} {Num(box.Y)} {Num(box.Width)} {Num(box.Height)}"));

        // THE ARROW HEADS FIRST, because a marker has to be defined before anything refers to it - and defined ONCE
        // however many arrows use it, which is what a marker is for.
        var defs = new XElement(Svg + "defs");

        // BY THE NUMBER THE ITEM CARRIES, not by the order the list happens to be in. A document is read top to bottom,
        // so writing it in any other order would be writing a different drawing - and the list handed in may be a
        // selection, picked up in whatever order the person clicked. Controls and wires are left out on the way here,
        // which is exactly why the number is also written down: the places left behind are holes, and a file that only
        // said "these five, in this order" could not put them back where they were.
        var ordered = new List<ICanvasItem>(items);
        ordered.Sort(static (ICanvasItem a, ICanvasItem b) => a.Order.CompareTo(b.Order));

        foreach (var item in ordered)
        {
            Write(root, item, defs);

            // What Write just put down, which is one element or none - asked of the end of the document rather than by
            // counting, so writing out a big drawing does not cost a walk per item.
            if (root.LastNode is XElement written)
            {
                written.SetAttributeValue("data-adm-order", item.Order.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (defs.HasElements) root.AddFirst(defs);

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString();
    }

    /// <summary>Reads a document back into items. Anything it does not understand is LEFT OUT rather than guessed at:
    /// a drawing that comes back subtly wrong is worse than one that comes back short and says so.</summary>
    /// <param name="skipped">How many elements were passed over - what a caller tells the user about.</param>
    public static IReadOnlyList<ICanvasItem> Load(string svg, out int skipped)
    {
        skipped = 0;
        var made = new List<ICanvasItem>();

        if (String.IsNullOrWhiteSpace(svg)) return made;

        XDocument document;
        try
        {
            document = XDocument.Parse(svg);
        }
        catch (System.Xml.XmlException)
        {
            // NOT an exception of ours to throw: a file somebody chose in a dialog being malformed is an ordinary
            // answer, and the caller says so in its own words.
            return made;
        }

        if (document.Root == null) return made;

        var count = 0;
        var at = 0;
        foreach (var child in document.Root.Elements())
        {
            var item = Read(child, ref count);

            if (item == null) continue;

            // THE NUMBER FROM THE FILE where the file has one, the place in the document where it does not - which is
            // what a stranger's drawing gives us, and reading it top to bottom is the same answer. Kept rather than
            // renumbered because the holes are the point: a drawing written out of a scene that also held controls
            // comes back knowing which places were not its own, and the scene it lands in decides what that means.
            item.Order = (int)Number(child, "data-adm-order", at);
            at++;

            made.Add(item);
        }

        skipped = count;
        return made;
    }

    public static IReadOnlyList<ICanvasItem> Load(string svg) => Load(svg, out _);

    /// <summary>What belongs in a drawing file at all - see the note on this class about controls and wires.</summary>
    public static bool Drawable(ICanvasItem item) =>
        item is StrokeItem or ShapeItem or TextItem or CurveItem or GroupItem or PathItem;

    // ---------------------------------------------------------------- writing

    private static void Write(XElement into, ICanvasItem item, XElement defs)
    {
        switch (item)
        {
            case StrokeItem ink:
                into.Add(Ink(ink));
                break;

            case ShapeItem shape:
                into.Add(Shape(shape, defs));
                break;

            case TextItem text:
                into.Add(Words(text));
                break;

            case CurveItem curve:
                into.Add(Curve(curve));
                break;

            case PathItem path:
                into.Add(Contour(path));
                break;

            case GroupItem group:
            {
                var g = new XElement(Svg + "g", new XAttribute("data-adm", "group"));

                foreach (var child in group.Children) Write(g, child, defs);

                into.Add(g);
                break;
            }
        }
    }

    private static XElement Ink(StrokeItem ink)
    {
        var points = new StringBuilder();

        foreach (var point in ink.Points)
        {
            if (points.Length > 0) points.Append(' ');
            points.Append(Num(ink.Origin.X + point.At.X)).Append(',').Append(Num(ink.Origin.Y + point.At.Y));
        }

        // ROUND caps and joins, because that is what a pen drawing is: a stroke written with the default butt caps
        // comes back as a chain of flat-ended segments with notches at every bend.
        var element = new XElement(Svg + "polyline",
            new XAttribute("data-adm", "stroke"),
            new XAttribute("points", points.ToString()),
            new XAttribute("fill", "none"),
            new XAttribute("stroke-linecap", "round"),
            new XAttribute("stroke-linejoin", "round"),
            new XAttribute("stroke-width", Num(ink.Thickness)));

        Paint(element, "stroke", ink.Brush);
        return element;
    }

    private static XElement Shape(ShapeItem shape, XElement defs)
    {
        var box = shape.World;
        XElement element;

        switch (shape.Shape)
        {
            case CanvasShape.Ellipse:
                element = new XElement(Svg + "ellipse",
                    new XAttribute("cx", Num(box.X + box.Width / 2)),
                    new XAttribute("cy", Num(box.Y + box.Height / 2)),
                    new XAttribute("rx", Num(box.Width / 2)),
                    new XAttribute("ry", Num(box.Height / 2)));
                break;

            case CanvasShape.Line:
            case CanvasShape.Arrow:
            {
                var ends = shape.Points;
                var from = ends.Count > 0 ? ends[0] : new Vector2(box.X, box.Y);
                var to = ends.Count > 1 ? ends[1] : new Vector2(box.X + box.Width, box.Y + box.Height);

                element = new XElement(Svg + "line",
                    new XAttribute("x1", Num(from.X)), new XAttribute("y1", Num(from.Y)),
                    new XAttribute("x2", Num(to.X)), new XAttribute("y2", Num(to.Y)));

                if (shape.Shape == CanvasShape.Arrow)
                {
                    element.Add(new XAttribute("data-adm", "arrow"),
                        new XAttribute("data-adm-heads", $"{shape.StartHead},{shape.EndHead}"),
                        new XAttribute("data-adm-head", $"{Num(shape.HeadLength)},{Num(shape.HeadWidth)}"));

                    Head(element, defs, shape.StartHead, shape.Stroke, true, shape.HeadLength, shape.HeadWidth);
                    Head(element, defs, shape.EndHead, shape.Stroke, false, shape.HeadLength, shape.HeadWidth);
                }
                break;
            }

            case CanvasShape.Polygon:
            {
                // THE SAME CORNERS THE FILL USES. Asked of the shape rather than worked out again here - where they
                // sit is a rule with a reason behind it (they are on the ellipse the shape is FITTED into, not the one
                // its box holds), and a second statement of a rule is a second rule.
                var points = new StringBuilder();

                foreach (var corner in shape.Outline)
                {
                    if (points.Length > 0) points.Append(' ');
                    points.Append(Num(corner.X)).Append(',').Append(Num(corner.Y));
                }

                element = new XElement(Svg + "polygon",
                    new XAttribute("points", points.ToString()),
                    new XAttribute("data-adm", "polygon"),
                    new XAttribute("data-adm-sides", shape.Sides.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("data-adm-box", Box(box)));
                break;
            }

            default:
            {
                var corner = shape.Corner;
                var four = $"{Num(corner.TopLeft)},{Num(corner.TopRight)},{Num(corner.BottomRight)},{Num(corner.BottomLeft)}";
                var alike = corner.TopLeft == corner.TopRight && corner.TopRight == corner.BottomRight
                            && corner.BottomRight == corner.BottomLeft;

                // SVG rounds a <rect> with ONE radius, and ours has four. Alike, that is exactly what a rect says;
                // unalike, it is not sayable at all - written as a rect anyway, every corner came out the size of the
                // top-left one for everybody but us. So an unequal shape goes out as its own OUTLINE, which any viewer
                // draws as drawn, with the four numbers riding alongside so it comes home as a rectangle and not as a
                // path.
                if (alike)
                {
                    element = new XElement(Svg + "rect",
                        new XAttribute("x", Num(box.X)), new XAttribute("y", Num(box.Y)),
                        new XAttribute("width", Num(box.Width)), new XAttribute("height", Num(box.Height)));

                    if (corner.TopLeft > 0)
                    {
                        element.Add(new XAttribute("rx", Num(corner.TopLeft)));
                        element.Add(new XAttribute("data-adm-corners", four));
                    }
                }
                else
                {
                    element = new XElement(Svg + "path",
                        new XAttribute("d", Outline(box, corner)),
                        new XAttribute("data-adm", "roundrect"),
                        new XAttribute("data-adm-box", Box(box)),
                        new XAttribute("data-adm-corners", four));
                }

                break;
            }
        }

        Paint(element, "fill", shape.Fill);
        Paint(element, "stroke", shape.Stroke);
        element.Add(new XAttribute("stroke-width", Num(shape.Thickness)));

        Turn(element, shape.Transform, new Vector2(box.X + box.Width / 2, box.Y + box.Height / 2));

        return element;
    }

    // A rounded box as a contour: four sides and a quarter-circle at each corner. Each radius is held to half the side
    // it sits on, the way a rounded rectangle is drawn everywhere - two corners asking for more than the side between
    // them would otherwise cross and tie the outline in a knot.
    private static string Outline(Rect box, CornerRadius corner)
    {
        var limit = Math.Min(box.Width, box.Height) / 2;
        var tl = Math.Min(Math.Max(0, corner.TopLeft), limit);
        var tr = Math.Min(Math.Max(0, corner.TopRight), limit);
        var br = Math.Min(Math.Max(0, corner.BottomRight), limit);
        var bl = Math.Min(Math.Max(0, corner.BottomLeft), limit);

        var right = box.X + box.Width;
        var bottom = box.Y + box.Height;

        return $"M {Num(box.X + tl)} {Num(box.Y)}"
               + $" H {Num(right - tr)}"
               + (tr > 0 ? $" A {Num(tr)} {Num(tr)} 0 0 1 {Num(right)} {Num(box.Y + tr)}" : "")
               + $" V {Num(bottom - br)}"
               + (br > 0 ? $" A {Num(br)} {Num(br)} 0 0 1 {Num(right - br)} {Num(bottom)}" : "")
               + $" H {Num(box.X + bl)}"
               + (bl > 0 ? $" A {Num(bl)} {Num(bl)} 0 0 1 {Num(box.X)} {Num(bottom - bl)}" : "")
               + $" V {Num(box.Y + tl)}"
               + (tl > 0 ? $" A {Num(tl)} {Num(tl)} 0 0 1 {Num(box.X + tl)} {Num(box.Y)}" : "")
               + " Z";
    }

    // Its own data, unchanged - the contour came in as SVG's grammar and goes out as the same, so a drawing that has
    // only been moved about comes back identical. Where it stands rides alongside, since the data says nothing about
    // where a person put it.
    private static XElement Contour(PathItem path)
    {
        var element = new XElement(Svg + "path",
            new XAttribute("d", path.Data ?? string.Empty),
            new XAttribute("data-adm", "path"),
            new XAttribute("data-adm-box", Box(path.World)),
            new XAttribute("fill-rule", path.FillRule == FillRule.NonZero ? "nonzero" : "evenodd"));

        Paint(element, "fill", path.Fill);
        Paint(element, "stroke", path.Stroke);

        if (path.Thickness > 0) element.Add(new XAttribute("stroke-width", Num(path.Thickness)));

        Turn(element, path.Transform, new Vector2(path.World.X + path.World.Width / 2,
            path.World.Y + path.World.Height / 2));

        return element;
    }

    private static XElement Words(TextItem text)
    {
        var element = new XElement(Svg + "text",
            new XAttribute("data-adm", "text"),
            new XAttribute("x", Num(text.Origin.X)),
            new XAttribute("y", Num(text.Origin.Y)),
            new XAttribute("font-size", Num(text.FontSize)),
            new XText(text.Text ?? String.Empty));

        // THE FAMILY AS THE FONT ITSELF NAMES IT. A FontFamily is built from a name and does not keep it, so the name
        // written out is the one the face reports - which is also the one another program will look for.
        if (text.FontFamily?.Typeface?.GetFont(0)?.TypographicFamilyName is { Length: > 0 } family)
        {
            element.Add(new XAttribute("font-family", family));
        }

        Paint(element, "fill", text.Brush);
        return element;
    }

    private static XElement Curve(CurveItem curve)
    {
        // WALKED, not stated: SVG has no B-spline and no NURBS, so what everyone else gets is the line the curve
        // actually draws. The control points ride alongside, so opening it here gives back the curve itself - still
        // editable by its own handles - rather than the polyline it was flattened into.
        var drawn = new StringBuilder();

        foreach (var point in curve.Sampled)
        {
            if (drawn.Length > 0) drawn.Append(' ');
            drawn.Append(Num(point.X)).Append(',').Append(Num(point.Y));
        }

        var control = new StringBuilder();

        foreach (var point in curve.Points)
        {
            if (control.Length > 0) control.Append(' ');
            control.Append(Num(point.X)).Append(',').Append(Num(point.Y));
        }

        var element = new XElement(Svg + "polyline",
            new XAttribute("data-adm", "curve"),
            new XAttribute("data-adm-kind", curve.Kind.ToString()),
            new XAttribute("data-adm-points", control.ToString()),
            new XAttribute("points", drawn.ToString()),
            new XAttribute("fill", "none"),
            new XAttribute("stroke-linecap", "round"),
            new XAttribute("stroke-linejoin", "round"),
            new XAttribute("stroke-width", Num(curve.Thickness)));

        Paint(element, "stroke", curve.Stroke);
        return element;
    }

    // One marker per head shape, color and SIZE - two arrows alike share it, and one with a longer head gets its own
    // rather than quietly wearing somebody else's.
    //
    // The numbers are the canvas's own: a head is measured in LINE THICKNESSES (see ArrowHead), and so is a marker -
    // markerUnits defaults to strokeWidth - so they go in as they are. Written as a fixed 6 by 6 out of a 10 by 10
    // view, every head came out about twice as long and twice as wide as the one on the plane, and on a thick line
    // that is not a head any more, it is a pair of bars across the end of the arrow.
    //
    // ONE PATH for both ends, pointing along the line: orient="auto-start-reverse" turns it round for marker-start.
    // Mirroring the path by hand as well turned it twice, so the head at the start pointed back down its own arrow.
    private static void Head(XElement arrow, XElement defs, CanvasArrowHead head, Brush brush, bool start,
        double length, double width)
    {
        if (head == CanvasArrowHead.None || defs == null) return;

        var color = Hex(brush) ?? "#000000";
        var id = $"adm-{head}-{color.TrimStart('#')}-{Num(length)}-{Num(width)}"
            .ToLowerInvariant().Replace('.', '_');

        if (Find(defs, id) == null)
        {
            var path = head == CanvasArrowHead.Barbs
                ? $"M 0 0 L {Num(length)} {Num(width / 2)} L 0 {Num(width)}"
                : $"M 0 0 L {Num(length)} {Num(width / 2)} L 0 {Num(width)} Z";

            defs.Add(new XElement(Svg + "marker",
                new XAttribute("id", id),
                new XAttribute("viewBox", $"0 0 {Num(length)} {Num(width)}"),
                new XAttribute("refX", Num(length)),
                new XAttribute("refY", Num(width / 2)),
                new XAttribute("markerWidth", Num(length)),
                new XAttribute("markerHeight", Num(width)),
                new XAttribute("orient", "auto-start-reverse"),
                new XElement(Svg + "path",
                    new XAttribute("d", path),
                    new XAttribute("fill", head == CanvasArrowHead.Barbs ? "none" : color),
                    new XAttribute("stroke", color),
                    new XAttribute("stroke-width", head == CanvasArrowHead.Barbs ? "1" : "0"))));
        }

        arrow.Add(new XAttribute(start ? "marker-start" : "marker-end", $"url(#{id})"));
    }

    private static XElement Find(XElement defs, string id)
    {
        foreach (var marker in defs.Elements(Svg + "marker"))
        {
            if ((string)marker.Attribute("id") == id) return marker;
        }

        return null;
    }

    private static void Turn(XElement element, CanvasTransform turn, Vector2 about)
    {
        if (turn.Angle == 0 && turn.SkewX == 0 && turn.SkewY == 0) return;

        var m = turn.Matrix(about);

        element.Add(new XAttribute("transform",
            $"matrix({Num(m.M11)} {Num(m.M12)} {Num(m.M21)} {Num(m.M22)} {Num(m.M41)} {Num(m.M42)})"));
    }

    private static void Paint(XElement element, string attribute, Brush brush)
    {
        var color = Hex(brush);

        element.Add(new XAttribute(attribute, color ?? "none"));

        if (brush is SolidColorBrush solid)
        {
            var alpha = solid.Color.A / 255.0 * solid.Opacity;

            if (alpha < 1) element.Add(new XAttribute($"{attribute}-opacity", Num(alpha)));
        }
    }

    private static string Hex(Brush brush) =>
        brush is SolidColorBrush solid ? $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}" : null;

    private static string Box(Rect box) => $"{Num(box.X)},{Num(box.Y)},{Num(box.Width)},{Num(box.Height)}";

    private static Rect Around(IReadOnlyList<ICanvasItem> items)
    {
        if (items.Count == 0) return new Rect(0, 0, 1, 1);

        double left = Double.MaxValue, top = Double.MaxValue, right = Double.MinValue, bottom = Double.MinValue;

        foreach (var item in items)
        {
            var box = item.Bounds;

            left = Math.Min(left, box.X);
            top = Math.Min(top, box.Y);
            right = Math.Max(right, box.X + box.Width);
            bottom = Math.Max(bottom, box.Y + box.Height);
        }

        // A MARGIN, because a stroke is drawn ON its line: a document cut to the geometry clips half the width of
        // everything that touches the edge.
        const double margin = 8;

        return new Rect(left - margin, top - margin,
            Math.Max(right - left + margin * 2, 1), Math.Max(bottom - top + margin * 2, 1));
    }

    private static string Num(double value) =>
        Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- reading

    private static ICanvasItem Read(XElement element, ref int skipped)
    {
        Styled(element);

        var made = Made(element, ref skipped);

        // ...and then put where its own transform says. NOT for a group: its children have already composed it into
        // theirs (see Inherit), and applying it again here would move everything twice.
        if (element.Name.LocalName != "g") Place(element, made);

        return made;
    }

    private static ICanvasItem Made(XElement element, ref int skipped)
    {
        var name = element.Name.LocalName;
        var mine = (string)element.Attribute("data-adm");

        switch (name)
        {
            case "g":
            {
                var children = new List<ICanvasItem>();

                foreach (var child in element.Elements())
                {
                    // WHAT THE GROUP SAYS, said to whoever has not said it themselves. Paint in SVG is INHERITED, and
                    // an icon states it once on the group round everything - read without this, every shape inside
                    // fell back on the defaults and the drawing came in black and hollow.
                    Inherit(element, child);

                    var made = Read(child, ref skipped);

                    if (made != null) children.Add(made);
                }

                // A <g> is a grouping in SVG whether or not anybody meant it as one of ours; an empty one is nothing.
                return children.Count > 0 ? new GroupItem(children) : null;
            }

            case "polyline":
            case "polygon":
                return Poly(element, name == "polygon", mine);

            case "rect":
                return Box(element);

            case "ellipse":
            case "circle":
                return Round(element);

            case "line":
                return Run(element, mine);

            case "text":
                return Say(element);

            case "path":
                // Ours when it says so: a box whose corners differ cannot be a <rect>, so it goes out as an outline -
                // and comes back a rectangle rather than a traced path.
                return mine == "roundrect" ? Box(element) : Trace(element, ref skipped);

            case "defs":
            case "title":
            case "desc":
            case "metadata":
                return null;   // said nothing about the picture, so nothing was skipped

            default:
                skipped++;
                return null;
        }
    }

    private static ICanvasItem Poly(XElement element, bool closed, string mine)
    {
        var points = Points((string)element.Attribute("points"));

        if (points.Count < 2) return null;

        var stroke = Fill(element, "stroke") ?? Brushes.Black;
        var width = Number(element, "stroke-width", 2);

        // A CURVE OF OURS says which kind it is and what its control points were; flattened, it would come back as a
        // line that cannot be reshaped by the handles that drew it.
        if (mine == "curve")
        {
            var control = Points((string)element.Attribute("data-adm-points"));

            if (control.Count >= 2 &&
                Enum.TryParse<CanvasCurve>((string)element.Attribute("data-adm-kind"), out var kind))
            {
                return new CurveItem(kind, control, stroke, width);
            }
        }

        if (mine == "polygon" || closed)
        {
            var box = Rectangle((string)element.Attribute("data-adm-box")) ?? Bounds(points);
            var sides = (int)Number(element, "data-adm-sides", points.Count);

            return new ShapeItem(CanvasShape.Polygon, box, stroke, width, Fill(element, "fill"))
            {
                Sides = Math.Max(3, sides)
            };
        }

        var ink = new StrokeItem(points[0], stroke, width);

        foreach (var point in points) ink.Add(point);

        return ink;
    }

    private static ICanvasItem Box(XElement element)
    {
        // An outline of ours carries the box it was drawn from; a plain <rect> states it in its own attributes.
        var box = Rectangle((string)element.Attribute("data-adm-box"))
                  ?? new Rect(Number(element, "x", 0), Number(element, "y", 0),
                      Number(element, "width", 0), Number(element, "height", 0));

        if (box.Width <= 0 || box.Height <= 0) return null;

        var shape = new ShapeItem(CanvasShape.Rectangle, box, Fill(element, "stroke"),
            Number(element, "stroke-width", 2), Fill(element, "fill"))
        {
            Corner = Corners(element)
        };

        return shape;
    }

    private static CornerRadius Corners(XElement element)
    {
        var four = (string)element.Attribute("data-adm-corners");

        if (four != null)
        {
            var parts = four.Split(',');

            if (parts.Length == 4)
            {
                return new CornerRadius(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]), Parse(parts[3]));
            }
        }

        var radius = Number(element, "rx", Number(element, "ry", 0));

        return new CornerRadius(radius);
    }

    private static ICanvasItem Round(XElement element)
    {
        var radius = Number(element, "r", 0);
        var rx = radius > 0 ? radius : Number(element, "rx", 0);
        var ry = radius > 0 ? radius : Number(element, "ry", 0);

        if (rx <= 0 || ry <= 0) return null;

        var cx = Number(element, "cx", 0);
        var cy = Number(element, "cy", 0);

        return new ShapeItem(CanvasShape.Ellipse, new Rect(cx - rx, cy - ry, rx * 2, ry * 2),
            Fill(element, "stroke"), Number(element, "stroke-width", 2), Fill(element, "fill"));
    }

    private static ICanvasItem Run(XElement element, string mine)
    {
        var from = new Vector2(Number(element, "x1", 0), Number(element, "y1", 0));
        var to = new Vector2(Number(element, "x2", 0), Number(element, "y2", 0));
        var arrow = mine == "arrow" || element.Attribute("marker-end") != null ||
                    element.Attribute("marker-start") != null;

        var box = new Rect(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y),
            Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));

        var shape = new ShapeItem(arrow ? CanvasShape.Arrow : CanvasShape.Line, box,
            Fill(element, "stroke") ?? Brushes.Black, Number(element, "stroke-width", 2))
        {
            // The box is the diagonal, so which way round the line runs inside it is the other half of the answer.
            Flipped = to.Y < from.Y != (to.X < from.X),
            Reversed = to.X < from.X || (Math.Abs(to.X - from.X) < 1e-9 && to.Y < from.Y)
        };

        var heads = ((string)element.Attribute("data-adm-heads"))?.Split(',');

        if (heads is { Length: 2 })
        {
            if (Enum.TryParse<CanvasArrowHead>(heads[0], out var start)) shape.StartHead = start;
            if (Enum.TryParse<CanvasArrowHead>(heads[1], out var end)) shape.EndHead = end;
        }
        else if (!arrow)
        {
            shape.StartHead = CanvasArrowHead.None;
            shape.EndHead = CanvasArrowHead.None;
        }

        // How big the head is, in line thicknesses - a drawing read back without it came home with every arrow wearing
        // the default head instead of the one it was saved with.
        var size = ((string)element.Attribute("data-adm-head"))?.Split(',');

        if (size is { Length: 2 })
        {
            if (double.TryParse(size[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var length))
                shape.HeadLength = length;
            if (double.TryParse(size[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var across))
                shape.HeadWidth = across;
        }

        return shape;
    }

    private static ICanvasItem Say(XElement element)
    {
        var words = element.Value;

        if (String.IsNullOrEmpty(words)) return null;

        var family = (string)element.Attribute("font-family");

        return new TextItem(new Vector2(Number(element, "x", 0), Number(element, "y", 0)), words,
            Fill(element, "fill") ?? Brushes.Black, Number(element, "font-size", 16),
            String.IsNullOrWhiteSpace(family) ? null : new FontFamily(family));
    }

    // A PATH is where somebody else's drawing arrives. Straight runs and cubics are followed exactly; an arc is not,
    // and a path holding one is left out rather than drawn as the chords nobody asked for.
    // A PATH COMES IN AS A PATH. Read by the engine's own SVGParser - the one reader of path data there is - so it
    // arrives with its sub-paths apart, its arcs as arcs and its fill rule intact. Walked into a run of points instead
    // it became one stroke: no fill, no holes, and a line joining the end of every sub-path to the start of the next.
    private static ICanvasItem Trace(XElement element, ref int skipped)
    {
        var data = (string)element.Attribute("d");

        if (String.IsNullOrWhiteSpace(data))
        {
            skipped++;
            return null;
        }

        // SVG'S OWN DEFAULTS, which are not nothing: a shape with no fill stated is filled BLACK, a stroke with no
        // width stated is one unit wide, and the fill rule is NONZERO. Read as "none, none, evenodd", a drawing came in
        // with its filled shapes hollow, its outlines missing and its holes in the wrong places.
        var stroke = Fill(element, "stroke");
        var fill = element.Attribute("fill") == null ? Brushes.Black : Fill(element, "fill");

        var path = new PathItem(data, fill, stroke, stroke == null ? 0 : Number(element, "stroke-width", 1))
        {
            FillRule = (string)element.Attribute("fill-rule") == "evenodd" ? FillRule.EvenOdd : FillRule.NonZero
        };

        if (path.World.Width <= 0 || path.World.Height <= 0)
        {
            skipped++;
            return null;
        }

        // Where the drawing put it, when it says so - a path of ours carries its box, since resizing one moves the
        // contour into a box the data itself knows nothing about.
        if (Rectangle((string)element.Attribute("data-adm-box")) is { } box) path.World = box;

        return path;
    }

    private static List<Vector2> Points(string text)
    {
        var points = new List<Vector2>();

        if (String.IsNullOrWhiteSpace(text)) return points;

        var parts = text.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            points.Add(new Vector2(Parse(parts[i]), Parse(parts[i + 1])));
        }

        return points;
    }

    private static Rect Bounds(IReadOnlyList<Vector2> points)
    {
        double left = Double.MaxValue, top = Double.MaxValue, right = Double.MinValue, bottom = Double.MinValue;

        foreach (var point in points)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        return new Rect(left, top, Math.Max(right - left, 1), Math.Max(bottom - top, 1));
    }

    private static Rect? Rectangle(string text)
    {
        if (String.IsNullOrWhiteSpace(text)) return null;

        var parts = text.Split(',');

        return parts.Length == 4
            ? new Rect(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]), Parse(parts[3]))
            : null;
    }

    private static double Number(XElement element, string attribute, double fallback)
    {
        var text = (string)element.Attribute(attribute);

        return String.IsNullOrWhiteSpace(text) ? fallback : Parse(text, fallback);
    }

    private static double Parse(string text, double fallback = 0)
    {
        if (String.IsNullOrWhiteSpace(text)) return fallback;

        // A LENGTH may carry its unit. Only the user units a canvas deals in are taken; a value in millimetres would
        // have to be converted against a page size this has no idea about.
        var cut = text.Trim().TrimEnd('p', 'x', 'P', 'X');

        return Double.TryParse(cut, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    // The paint attributes SVG inherits, handed from a group to a child that has none of its own. Written onto the
    // child rather than carried alongside: the readers below ask the element they are given, and a second way of
    // saying where a value came from would be a second set of rules to keep in step.
    private static readonly string[] Inherited =
        { "fill", "stroke", "stroke-width", "fill-rule", "fill-opacity", "stroke-opacity", "opacity" };

    // WHAT THE style ATTRIBUTE SAYS, said as attributes. Every drawing tool writes paint there rather than beside it -
    // style="fill:#1e73be;fill-rule:evenodd" - and read only from attributes a drawing arrived with the wrong colors
    // and the wrong rule, which shows up as a shape filled inside out. Style WINS over an attribute of the same name,
    // which is what CSS says and what every viewer does.
    private static void Styled(XElement element)
    {
        if (element.Attribute("style") is not { } style || String.IsNullOrWhiteSpace(style.Value)) return;

        foreach (var pair in style.Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = pair.IndexOf(':');

            if (colon <= 0) continue;

            var name = pair[..colon].Trim().ToLowerInvariant();
            var value = pair[(colon + 1)..].Trim();

            if (Array.IndexOf(Inherited, name) >= 0 && value.Length > 0) element.SetAttributeValue(name, value);
        }
    }

    private static void Inherit(XElement parent, XElement child)
    {
        foreach (var name in Inherited)
        {
            if (parent.Attribute(name) is { } said && child.Attribute(name) == null) child.Add(new XAttribute(name, said.Value));
        }

        // A TRANSFORM IS NOT INHERITED, IT COMPOSES: the group's is applied to the child's result, so the two are
        // multiplied and the child carries the answer. Left out entirely, everything under a group that moves or
        // scales its contents - which is most of an exported icon - landed at the coordinates the file never meant.
        if (parent.Attribute("transform") is not { } outer) return;

        var over = Affine(outer.Value);
        var own = child.Attribute("transform") is { } inner ? Affine(inner.Value) : Identity;

        child.SetAttributeValue("transform", AsMatrix(Times(over, own)));
    }

    private static readonly double[] Identity = { 1, 0, 0, 1, 0, 0 };

    // The transform list SVG writes, as one matrix: a b c d e f, applied as x' = a·x + c·y + e, y' = b·x + d·y + f.
    private static double[] Affine(string text)
    {
        var result = Identity;

        if (String.IsNullOrWhiteSpace(text)) return result;

        foreach (Match match in Regex.Matches(text, @"(\w+)\s*\(([^)]*)\)"))
        {
            var numbers = new List<double>();

            foreach (var part in match.Groups[2].Value.Split(new[] { ',', ' ', '\t', '\n', '\r' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (Double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    numbers.Add(number);
            }

            result = Times(result, Named(match.Groups[1].Value, numbers));
        }

        return result;
    }

    private static double[] Named(string what, List<double> n) => what switch
    {
        "translate" => new[] { 1, 0, 0, 1.0, n.Count > 0 ? n[0] : 0, n.Count > 1 ? n[1] : 0 },
        "scale" => new[] { n.Count > 0 ? n[0] : 1, 0, 0, n.Count > 1 ? n[1] : n.Count > 0 ? n[0] : 1, 0, 0.0 },
        "matrix" when n.Count >= 6 => new[] { n[0], n[1], n[2], n[3], n[4], n[5] },
        "rotate" when n.Count >= 1 => Turned(n),
        _ => Identity
    };

    private static double[] Turned(List<double> n)
    {
        var radians = n[0] * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var about = new[] { cos, sin, -sin, cos, 0, 0.0 };

        if (n.Count < 3) return about;

        // rotate(a x y) turns about a point, which is a move there, the turn, and a move back.
        return Times(Times(new[] { 1, 0, 0, 1.0, n[1], n[2] }, about), new[] { 1, 0, 0, 1.0, -n[1], -n[2] });
    }

    private static double[] Times(double[] first, double[] second) => new[]
    {
        first[0] * second[0] + first[2] * second[1],
        first[1] * second[0] + first[3] * second[1],
        first[0] * second[2] + first[2] * second[3],
        first[1] * second[2] + first[3] * second[3],
        first[0] * second[4] + first[2] * second[5] + first[4],
        first[1] * second[4] + first[3] * second[5] + first[5]
    };

    private static string AsMatrix(double[] m) =>
        $"matrix({Num(m[0])},{Num(m[1])},{Num(m[2])},{Num(m[3])},{Num(m[4])},{Num(m[5])})";

    // Puts a finished item where its transform says. The box is carried through the matrix - which is exact for a
    // move and a scale, the two an exported drawing is made of - and a turn or a lean is handed to the item itself
    // where it can hold one.
    private static void Place(XElement element, ICanvasItem item)
    {
        if (item == null || element.Attribute("transform") is not { } said) return;

        var m = Affine(said.Value);

        if (m[0] == 1 && m[1] == 0 && m[2] == 0 && m[3] == 1 && m[4] == 0 && m[5] == 0) return;

        var box = item.Bounds;
        var corners = new[]
        {
            Mapped(m, box.X, box.Y), Mapped(m, box.X + box.Width, box.Y),
            Mapped(m, box.X, box.Y + box.Height), Mapped(m, box.X + box.Width, box.Y + box.Height)
        };

        double left = Double.MaxValue, top = Double.MaxValue, right = Double.MinValue, bottom = Double.MinValue;

        foreach (var corner in corners)
        {
            left = Math.Min(left, corner.X);
            top = Math.Min(top, corner.Y);
            right = Math.Max(right, corner.X);
            bottom = Math.Max(bottom, corner.Y);
        }

        item.Resize(new Rect(left, top, Math.Max(1e-9, right - left), Math.Max(1e-9, bottom - top)));

        if (item is not ICanvasTransformed turned || (m[1] == 0 && m[2] == 0)) return;

        turned.Transform = new CanvasTransform
        {
            Angle = Math.Atan2(m[1], m[0]) * 180 / Math.PI
        };
    }

    private static Vector2 Mapped(double[] m, double x, double y) =>
        new(m[0] * x + m[2] * y + m[4], m[1] * x + m[3] * y + m[5]);

    private static Brush Fill(XElement element, string attribute)
    {
        var text = (string)element.Attribute(attribute);

        if (String.IsNullOrWhiteSpace(text) || text == "none") return null;

        var color = CanvasSvgColor.Of(text);

        if (color == null) return null;

        var brush = new SolidColorBrush(color.Value);
        var alpha = Number(element, $"{attribute}-opacity", 1);

        if (alpha < 1) brush.Opacity = alpha;

        return brush;
    }
}
