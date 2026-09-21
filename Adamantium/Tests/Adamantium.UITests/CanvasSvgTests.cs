using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A DRAWING AS SVG, out and back. Two readers are served at once: a stranger's viewer, which knows only the
/// standard elements, and this one, which reads the <c>data-</c> attributes beside them and gets the object back
/// exactly. Both halves are worth pinning - a file that only WE can open is not why a drawing is written as SVG.
/// </summary>
[TestFixture]
public class CanvasSvgTests
{
    private static IReadOnlyList<ICanvasItem> Round(params ICanvasItem[] items)
    {
        var svg = CanvasSvg.Save(items);

        Assert.That(svg, Does.Contain("http://www.w3.org/2000/svg"), "what came out is not an SVG document");

        return CanvasSvg.Load(svg, out var skipped).Also(() =>
            Assert.That(skipped, Is.Zero, "our own document held something we could not read back"));
    }

    private static StrokeItem Ink()
    {
        var ink = new StrokeItem(new Vector2(10, 20), new SolidColorBrush(Colors.Tomato), 3);

        ink.Add(new Vector2(10, 20));
        ink.Add(new Vector2(40, 60));
        ink.Add(new Vector2(70, 25));

        return ink;
    }

    // WHAT A STRANGER SEES. Every item is written as the element that says it, so a viewer with no knowledge of us
    // draws the picture correctly - that is the whole reason a drawing is SVG and not a format of our own.
    //
    // ADAM_SVG_DUMP writes the document out, so the claim can be checked the only way it really can be: by opening it
    // in something that is not us.
    [Test]
    public void EachKindIsWrittenAsTheElementThatSaysIt()
    {
        var svg = CanvasSvg.Save(new ICanvasItem[]
        {
            Ink(),
            new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 40, 30), Brushes.Black, 2),
            new ShapeItem(CanvasShape.Ellipse, new Rect(50, 0, 40, 30), Brushes.Black, 2),
            new ShapeItem(CanvasShape.Line, new Rect(0, 50, 40, 30), Brushes.Black, 2),
            new ShapeItem(CanvasShape.Polygon, new Rect(50, 50, 40, 40), Brushes.Black, 2) { Sides = 6 },
            new TextItem(new Vector2(5, 120), "Hello", Brushes.Black, 14),
            new CurveItem(CanvasCurve.Bezier, new[] { new Vector2(0, 0), new Vector2(20, 40), new Vector2(60, 0) },
                Brushes.Black, 2)
        });

        if (System.Environment.GetEnvironmentVariable("ADAM_SVG_DUMP") is { Length: > 0 } where)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(where, "drawing.svg"), svg);
        }

        Assert.Multiple(() =>
        {
            Assert.That(svg, Does.Contain("<polyline"), "a stroke is a polyline everywhere");
            Assert.That(svg, Does.Contain("<rect"));
            Assert.That(svg, Does.Contain("<ellipse"));
            Assert.That(svg, Does.Contain("<line"));
            Assert.That(svg, Does.Contain("<polygon"));
            Assert.That(svg, Does.Contain("<text"));
            Assert.That(svg, Does.Contain("Hello"), "the words themselves are the point of a text element");
        });
    }

    [Test]
    public void AStrokeComesBackWithItsPointsAndItsPen()
    {
        var back = Round(Ink()).OfType<StrokeItem>().SingleOrDefault();

        Assert.That(back, Is.Not.Null, "the stroke did not come back at all");

        Assert.Multiple(() =>
        {
            Assert.That(back.Points, Has.Count.EqualTo(3), "a point of the stroke was lost");
            Assert.That(back.Origin.X + back.Points[1].At.X, Is.EqualTo(40).Within(0.01), "the stroke moved");
            Assert.That(back.Origin.Y + back.Points[1].At.Y, Is.EqualTo(60).Within(0.01));
            Assert.That(back.Thickness, Is.EqualTo(3).Within(0.01), "the pen changed width");
            Assert.That((back.Brush as SolidColorBrush)?.Color, Is.EqualTo(Colors.Tomato), "the colour changed");
        });
    }

    [Test]
    public void ARectangleComesBackWithItsBoxAndItsFourCorners()
    {
        var made = new ShapeItem(CanvasShape.Rectangle, new Rect(12, 34, 100, 60), Brushes.Black, 4,
            new SolidColorBrush(Colors.CornflowerBlue))
        {
            Corner = new CornerRadius(2, 8, 16, 4)
        };

        var back = Round(made).OfType<ShapeItem>().SingleOrDefault();

        Assert.That(back, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(back.Shape, Is.EqualTo(CanvasShape.Rectangle));
            Assert.That(back.World.X, Is.EqualTo(12).Within(0.01));
            Assert.That(back.World.Width, Is.EqualTo(100).Within(0.01));
            Assert.That(back.Corner.TopRight, Is.EqualTo(8).Within(0.01), "SVG rounds with one radius - ours has four");
            Assert.That(back.Corner.BottomRight, Is.EqualTo(16).Within(0.01));
            Assert.That((back.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.CornflowerBlue));
        });
    }

    [Test]
    public void AnEllipseComesBackAsAnEllipseAndNotItsBox()
    {
        var back = Round(new ShapeItem(CanvasShape.Ellipse, new Rect(10, 20, 80, 40), Brushes.Black, 2))
            .OfType<ShapeItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(back.Shape, Is.EqualTo(CanvasShape.Ellipse));
            Assert.That(back.World.X, Is.EqualTo(10).Within(0.01));
            Assert.That(back.World.Width, Is.EqualTo(80).Within(0.01));
            Assert.That(back.World.Height, Is.EqualTo(40).Within(0.01));
        });
    }

    // A POLYGON is the case SVG has no parametric word for: everyone else gets the corners spelled out, and the number
    // of sides rides alongside so it comes back as a hexagon that can still be told to be a heptagon.
    [Test]
    public void APolygonKeepsItsSidesAndItsCornersLandWhereTheyAreDrawn()
    {
        var made = new ShapeItem(CanvasShape.Polygon, new Rect(0, 0, 90, 90), Brushes.Black, 2) { Sides = 6 };
        var svg = CanvasSvg.Save(new ICanvasItem[] { made });
        var back = CanvasSvg.Load(svg).OfType<ShapeItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(back.Sides, Is.EqualTo(6), "the polygon came back with a different number of sides");
            Assert.That(back.World.Width, Is.EqualTo(90).Within(0.01), "its box moved");
        });

        // ...and the corners in the file are the ones the shape draws, not a guess made from the box.
        foreach (var corner in made.Outline)
        {
            Assert.That(svg, Does.Contain($"{corner.X:0.####}".Replace(",", ".")),
                "a corner in the file is not one the shape draws");
        }
    }

    [Test]
    public void AnArrowKeepsItsHeadsAndPointsTheSameWay()
    {
        var made = new ShapeItem(CanvasShape.Arrow, new Rect(10, 10, 60, 40), Brushes.Black, 3)
        {
            StartHead = CanvasArrowHead.Barbs,
            EndHead = CanvasArrowHead.Triangle
        };

        var svg = CanvasSvg.Save(new ICanvasItem[] { made });

        Assert.That(svg, Does.Contain("marker"), "an arrow drawn elsewhere would have no head at all");

        var back = CanvasSvg.Load(svg).OfType<ShapeItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(back.Shape, Is.EqualTo(CanvasShape.Arrow));
            Assert.That(back.StartHead, Is.EqualTo(CanvasArrowHead.Barbs));
            Assert.That(back.EndHead, Is.EqualTo(CanvasArrowHead.Triangle));
            Assert.That(back.Points[0].X, Is.EqualTo(made.Points[0].X).Within(0.01), "the arrow turned round");
            Assert.That(back.Points[1].Y, Is.EqualTo(made.Points[1].Y).Within(0.01));
        });
    }

    // A HEAD IS THE SIZE THE PLANE DRAWS IT. Written as a fixed 6-by-6 marker, every arrow exported with a head about
    // twice as long and twice as wide as its own - and on a thick line that stops being a head at all and reads as a
    // pair of bars laid across the end of the arrow.
    [Test]
    public void AnArrowHeadIsExportedAtTheSizeItIsDrawn()
    {
        var made = new ShapeItem(CanvasShape.Arrow, new Rect(0, 0, 80, 0), Brushes.Black, 6)
        {
            StartHead = CanvasArrowHead.None,
            EndHead = CanvasArrowHead.Triangle,
            HeadLength = 3.2,
            HeadWidth = 2.6
        };

        var svg = CanvasSvg.Save(new ICanvasItem[] { made });

        Assert.Multiple(() =>
        {
            Assert.That(svg, Does.Contain("markerWidth=\"3.2\""), "the head is not as long as the one on the plane");
            Assert.That(svg, Does.Contain("markerHeight=\"2.6\""), "the head is not as wide as the one on the plane");

            // One path for both ends, turned round by the viewer - mirrored by hand as well, a head at the START
            // pointed back down its own arrow.
            Assert.That(svg, Does.Contain("auto-start-reverse"));
        });

        var back = CanvasSvg.Load(svg).OfType<ShapeItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(back.HeadLength, Is.EqualTo(3.2).Within(0.001), "it came home wearing the default head");
            Assert.That(back.HeadWidth, Is.EqualTo(2.6).Within(0.001), "it came home wearing the default head");
        });
    }

    // FOUR DIFFERENT CORNERS cannot be said by a <rect>: it rounds with one radius. Written as one anyway, every
    // corner came out the size of the top-left one in any viewer but ours - the four numbers rode alongside in an
    // attribute only we read. Unequal corners now go out as the shape's own outline, which anyone draws as drawn.
    [Test]
    public void ARectangleWithUnequalCornersIsExportedAsItIsDrawn()
    {
        var made = new ShapeItem(CanvasShape.Rectangle, new Rect(10, 20, 120, 80), Brushes.Black, 2, Brushes.Red)
        {
            Corner = new CornerRadius(12, 0, 30, 4)
        };

        var svg = CanvasSvg.Save(new ICanvasItem[] { made });

        Assert.Multiple(() =>
        {
            Assert.That(svg, Does.Contain("<path"), "unequal corners still went out as a rect");
            Assert.That(svg, Does.Not.Contain("<rect"), "a rect cannot say four different corners");
            Assert.That(svg, Does.Contain("A 12 12"), "the top-left corner is not in the outline");
            Assert.That(svg, Does.Contain("A 30 30"), "the bottom-right corner is not in the outline");
        });

        var back = CanvasSvg.Load(svg).OfType<ShapeItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(back.Shape, Is.EqualTo(CanvasShape.Rectangle), "it came home as a traced path");
            Assert.That(back.World.X, Is.EqualTo(10).Within(0.01));
            Assert.That(back.World.Width, Is.EqualTo(120).Within(0.01));
            Assert.That(back.Corner.TopLeft, Is.EqualTo(12).Within(0.01));
            Assert.That(back.Corner.TopRight, Is.EqualTo(0).Within(0.01));
            Assert.That(back.Corner.BottomRight, Is.EqualTo(30).Within(0.01));
            Assert.That(back.Corner.BottomLeft, Is.EqualTo(4).Within(0.01));
        });
    }

    // ...and equal ones stay a plain <rect>, which is what every viewer draws best.
    [Test]
    public void ARectangleWithEqualCornersStaysARect()
    {
        var made = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40), Brushes.Black, 2, Brushes.Red)
        {
            Corner = new CornerRadius(8)
        };

        var svg = CanvasSvg.Save(new ICanvasItem[] { made });

        Assert.That(svg, Does.Contain("<rect"));
        Assert.That(CanvasSvg.Load(svg).OfType<ShapeItem>().Single().Corner.BottomLeft, Is.EqualTo(8).Within(0.01));
    }

    // AN IMPORTED PATH IS A PATH. Walked into a run of points it became one stroke: no fill, no holes, and a line
    // joining the end of every sub-path to the start of the next - the web of stray strokes an icon arrived wearing.
    // Read by the engine's own SVGParser, the sub-paths stay apart and the arcs stay arcs.
    [Test]
    public void APathComesInAsAContourAndNotAsAStroke()
    {
        // A ring: an outer circle and an inner one, drawn as arcs, with the hole made by the fill rule.
        const string ring = "M 10 50 A 40 40 0 1 0 90 50 A 40 40 0 1 0 10 50 Z "
                            + "M 30 50 A 20 20 0 1 0 70 50 A 20 20 0 1 0 30 50 Z";

        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"{ring}\" fill=\"#ff0000\" "
                  + "fill-rule=\"evenodd\"/></svg>";

        var made = CanvasSvg.Load(svg, out var skipped);

        Assert.That(skipped, Is.EqualTo(0), "a path with arcs in it was refused");

        var path = made.OfType<PathItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(path.FillRule, Is.EqualTo(FillRule.EvenOdd), "the hole in the ring is made by the fill rule");
            Assert.That(path.World.Width, Is.GreaterThan(0), "it arrived with no size");
            Assert.That(path.Data, Does.Contain("A"), "the arcs did not survive the reading");
        });
    }

    // SVG'S OWN DEFAULTS AND ITS INHERITANCE. A shape that states no fill is filled BLACK, a stroke with no width is
    // one unit wide, and paint stated on the group round everything reaches the shapes inside it. Read as "none, none",
    // an icon came in hollow with its outlines missing - which is what "it does not draw all the lines" looks like.
    [Test]
    public void APathTakesTheDefaultsAndWhatTheGroupSays()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg">
                             <path d="M 0 0 L 10 0 L 10 10 Z"/>
                             <g fill="#00ff00" stroke="#0000ff">
                               <path d="M 20 0 L 30 0 L 30 10 Z"/>
                             </g>
                           </svg>
                           """;

        var made = CanvasSvg.Load(svg);
        var plain = made.OfType<PathItem>().First();
        var inside = made.OfType<GroupItem>().Single().Children.OfType<PathItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That((plain.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Black), "a shape with no fill is black");
            Assert.That(plain.FillRule, Is.EqualTo(FillRule.NonZero), "SVG fills by the nonzero rule unless told otherwise");

            Assert.That((inside.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Lime), "the group's fill did not reach it");
            Assert.That((inside.Stroke as SolidColorBrush)?.Color, Is.EqualTo(Colors.Blue), "the group's stroke did not reach it");
            Assert.That(inside.Thickness, Is.EqualTo(1).Within(0.001), "a stroke with no width stated is one unit wide");
        });
    }

    // PAINT LIVES IN style AS OFTEN AS BESIDE IT. Every drawing tool writes it there, and a reader that only looks at
    // attributes gets the wrong color and - worse - the wrong fill rule, which shows as a shape filled inside out.
    [Test]
    public void StyleIsReadAndBeatsTheAttributeBesideIt()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg">
                             <path d="M 0 0 L 10 0 L 10 10 Z" fill="#ff0000"
                                   style="fill:#0000ff;fill-rule:evenodd;stroke:#00ff00;stroke-width:3"/>
                           </svg>
                           """;

        var path = CanvasSvg.Load(svg).OfType<PathItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That((path.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Blue), "the attribute won over style");
            Assert.That(path.FillRule, Is.EqualTo(FillRule.EvenOdd), "the rule in style was not read");
            Assert.That((path.Stroke as SolidColorBrush)?.Color, Is.EqualTo(Colors.Lime));
            Assert.That(path.Thickness, Is.EqualTo(3).Within(0.001));
        });
    }

    // A TRANSFORM PUTS THE SHAPE WHERE THE FILE MEANS. An exported icon keeps its parts under groups that move and
    // scale them; read without transforms, every part landed at coordinates the drawing never meant - which is the
    // rest of "it parses crookedly".
    [Test]
    public void AShapeIsPutWhereItsTransformSays()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg">
                             <g transform="translate(100 50)">
                               <rect x="0" y="0" width="20" height="10" transform="scale(2)"/>
                             </g>
                           </svg>
                           """;

        var box = CanvasSvg.Load(svg).OfType<GroupItem>().Single().Children.OfType<ShapeItem>().Single();

        Assert.Multiple(() =>
        {
            // scale(2) first, then the group's move: (0,0)-(20,10) becomes (0,0)-(40,20), then +100,+50.
            Assert.That(box.World.X, Is.EqualTo(100).Within(0.01), "the group's move did not reach it");
            Assert.That(box.World.Y, Is.EqualTo(50).Within(0.01));
            Assert.That(box.World.Width, Is.EqualTo(40).Within(0.01), "its own scale was not applied");
            Assert.That(box.World.Height, Is.EqualTo(20).Within(0.01));
        });
    }

    // ...AND GOES OUT AS THE SAME PATH. Its own grammar came in, and the same grammar goes back - a drawing only moved
    // about must come home identical.
    [Test]
    public void APathIsWrittenBackAsItself()
    {
        const string shape = "M 0 0 L 40 0 L 40 30 Z M 60 0 L 100 0 L 100 30 Z";

        var made = new PathItem(shape, Brushes.Red, Brushes.Black, 2);
        var svg = CanvasSvg.Save(new ICanvasItem[] { made });
        var back = CanvasSvg.Load(svg).OfType<PathItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(back.Data, Is.EqualTo(shape), "it came home written some other way");
            Assert.That(back.World.Width, Is.EqualTo(made.World.Width).Within(0.01));
            Assert.That(back.World.Height, Is.EqualTo(made.World.Height).Within(0.01));
        });
    }

    // A CURVE is the other case SVG cannot state: it has no B-spline. The line it draws is written for everyone, and
    // the control points ride alongside - so here it comes back as a curve with handles, not as the flattened line.
    [Test]
    public void ACurveComesBackAsACurveAndNotAsItsFlattening()
    {
        var control = new[] { new Vector2(0, 0), new Vector2(30, 80), new Vector2(90, 10), new Vector2(120, 60) };
        var made = new CurveItem(CanvasCurve.BSpline, control, Brushes.Black, 2);
        var svg = CanvasSvg.Save(new ICanvasItem[] { made });

        Assert.That(svg, Does.Contain("<polyline"), "a stranger's viewer would see nothing of the curve");

        var back = CanvasSvg.Load(svg).OfType<CurveItem>().SingleOrDefault();

        Assert.That(back, Is.Not.Null, "the curve came back as something else");

        Assert.Multiple(() =>
        {
            Assert.That(back.Kind, Is.EqualTo(CanvasCurve.BSpline), "a B-spline came back as another kind of line");
            Assert.That(back.Points, Has.Count.EqualTo(control.Length), "the handles that shape it were lost");
            Assert.That(back.Points[1].X, Is.EqualTo(30).Within(0.01));
        });
    }

    [Test]
    public void TextComesBackWithItsWordsAndItsSize()
    {
        var back = Round(new TextItem(new Vector2(40, 90), "Where the plane has no edges", Brushes.Black, 18))
            .OfType<TextItem>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(back.Text, Is.EqualTo("Where the plane has no edges"));
            Assert.That(back.FontSize, Is.EqualTo(18).Within(0.01));
            Assert.That(back.Origin.Y, Is.EqualTo(90).Within(0.01));
        });
    }

    [Test]
    public void AGroupComesBackAsAGroupWithWhatWasInIt()
    {
        var group = new GroupItem(new ICanvasItem[]
        {
            new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 20, 20), Brushes.Black, 1),
            new ShapeItem(CanvasShape.Ellipse, new Rect(30, 0, 20, 20), Brushes.Black, 1)
        });

        var back = Round(group).OfType<GroupItem>().SingleOrDefault();

        Assert.That(back, Is.Not.Null, "the group was flattened");
        Assert.That(back.Children, Has.Count.EqualTo(2), "something in the group was lost");
    }

    // CONTROLS AND WIRES ARE NOT A DRAWING. A control is a living thing with a template and behaviour, and a rectangle
    // labelled "Button" in a file would be a lie about what it is.
    [Test]
    public void ControlsAreLeftOutRatherThanDrawnAsShapes()
    {
        Assert.That(CanvasSvg.Drawable(new ElementItem(new Adamantium.UI.Controls.Buttons.Button(),
            new Rect(0, 0, 10, 10))), Is.False);
    }

    // SOMEBODY ELSE'S FILE. Nothing here was written by us - no data- attributes at all - and it still has to open.
    [Test]
    public void AForeignDocumentOpens()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200">
                             <rect x="10" y="10" width="80" height="40" fill="#ff0000" stroke="blue"/>
                             <circle cx="150" cy="50" r="25" fill="none" stroke="rgb(0, 128, 0)"/>
                             <line x1="10" y1="120" x2="90" y2="180" stroke="#000"/>
                             <polyline points="100,120 140,160 180,120" fill="none" stroke="tomato"/>
                             <text x="20" y="195" font-size="12">from elsewhere</text>
                           </svg>
                           """;

        var items = CanvasSvg.Load(svg, out var skipped);

        Assert.That(skipped, Is.Zero, "a plain document had something we passed over");

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Count.EqualTo(5), "not everything in the document came through");

            var box = items.OfType<ShapeItem>().First(s => s.Shape == CanvasShape.Rectangle);
            Assert.That((box.Fill as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red), "#ff0000 is red");
            Assert.That((box.Stroke as SolidColorBrush)?.Color, Is.EqualTo(Colors.Blue), "a named colour was missed");

            var circle = items.OfType<ShapeItem>().First(s => s.Shape == CanvasShape.Ellipse);
            Assert.That(circle.World.Width, Is.EqualTo(50).Within(0.01), "a circle is an ellipse of its diameter");
            Assert.That((circle.Stroke as SolidColorBrush)?.Color, Is.EqualTo(Colors.Green), "rgb() was not read");

            Assert.That(items.OfType<TextItem>().Single().Text, Is.EqualTo("from elsewhere"));
        });
    }

    // A PATH is how most foreign drawings arrive. Straight runs and cubics are followed; what is not followed is said
    // so rather than drawn as the chords nobody asked for.
    [Test]
    public void APathIsFollowed()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg">
                             <path d="M 10 10 L 50 10 C 70 10 90 30 90 50 L 90 90 Z" stroke="black"/>
                           </svg>
                           """;

        var path = CanvasSvg.Load(svg, out var skipped).OfType<PathItem>().SingleOrDefault();

        Assert.That(skipped, Is.Zero);
        Assert.That(path, Is.Not.Null, "the path did not come through");

        Assert.Multiple(() =>
        {
            // KEPT AS A CURVE, not walked into a fan of points: the file said a cubic, so a cubic is what stands on the
            // plane and what goes back out.
            Assert.That(path.Data, Does.Contain("C"), "the cubic was flattened on the way in");
            Assert.That(path.Data, Does.Contain("Z"), "the path lost its closing");
            Assert.That(path.World.X, Is.EqualTo(10).Within(0.01), "the path did not start where it says");
        });
    }

    // AN ARC is its own piece of trigonometry, and the canvas used to refuse a path holding one - honestly, but it
    // meant half of every round icon was simply missing. The engine's parser does arcs, so there is nothing left to
    // refuse: what is skipped now is only what says nothing about the picture.
    [Test]
    public void AnArcIsReadRatherThanRefused()
    {
        const string svg = """
                           <svg xmlns="http://www.w3.org/2000/svg">
                             <path d="M 10 10 A 30 30 0 0 1 50 50" stroke="black"/>
                           </svg>
                           """;

        var items = CanvasSvg.Load(svg, out var skipped);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.Zero, "the arc was passed over");
            Assert.That(items.OfType<PathItem>().Single().Data, Does.Contain("A"), "it came back without its arc");
        });
    }

    // WRITTEN IN THE ORDER THE NUMBERS SAY, not in the order the list arrived. A document is read top to bottom, so
    // writing a selection in the order somebody clicked it would be writing a different drawing.
    [Test]
    public void ItIsWrittenInTheOrderTheNumbersSay()
    {
        var scene = new CanvasScene();
        var first = Ink();
        var second = Ink();
        var third = Ink();

        scene.Add(first);
        scene.Add(second);
        scene.Add(third);

        // Handed over backwards on purpose - which is what a selection picked up top-down looks like.
        var svg = CanvasSvg.Save(new ICanvasItem[] { third, second, first });
        var back = CanvasSvg.Load(svg);

        Assert.That(back, Has.Count.EqualTo(3));
        Assert.That(new[] { back[0].Order, back[1].Order, back[2].Order }, Is.EqualTo(new[] { 0, 1, 2 }),
            "the document came back in a different order from the one it was written in");
    }

    // THE NUMBER ITSELF SURVIVES, holes and all. A drawing written out of a scene that also held controls has gaps in
    // its numbering - those are the places the controls stood, and a file that renumbered them tight would have thrown
    // away the only record of where the drawing sat among them.
    [Test]
    public void TheNumberSurvivesTheRoundTrip()
    {
        var low = Ink();
        var high = Ink();

        low.Order = 2;
        high.Order = 9;

        var back = CanvasSvg.Load(CanvasSvg.Save(new ICanvasItem[] { high, low }));

        Assert.That(back, Has.Count.EqualTo(2));
        Assert.That(new[] { back[0].Order, back[1].Order }, Is.EqualTo(new[] { 2, 9 }));
    }

    // A STRANGER'S DRAWING has no numbers of ours in it, and the document's own order is the answer - which is what
    // reading it top to bottom means anyway.
    [Test]
    public void ADocumentWithoutOurNumbersIsReadTopToBottom()
    {
        var svg = """
                  <svg xmlns="http://www.w3.org/2000/svg">
                    <rect x="0" y="0" width="10" height="10" fill="#ff0000"/>
                    <rect x="20" y="0" width="10" height="10" fill="#00ff00"/>
                    <rect x="40" y="0" width="10" height="10" fill="#0000ff"/>
                  </svg>
                  """;

        var items = CanvasSvg.Load(svg);

        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(new[] { items[0].Order, items[1].Order, items[2].Order }, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void ANonsenseDocumentIsRefusedRatherThanThrown()
    {
        Assert.DoesNotThrow(() =>
        {
            var items = CanvasSvg.Load("this is not a document at all", out _);

            Assert.That(items, Is.Empty);
        });
    }
}

internal static class AlsoExtension
{
    // Runs an assertion beside a value and hands the value on - so a helper can both check and return.
    public static T Also<T>(this T value, System.Action check)
    {
        check();
        return value;
    }
}
