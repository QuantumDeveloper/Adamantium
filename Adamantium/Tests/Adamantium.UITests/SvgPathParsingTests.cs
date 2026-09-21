using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>THE PATH GRAMMAR, read as SVG states it. Every case here is one an exporter writes by default, and each
/// used to bend a drawing rather than break it - which is why an imported icon looked nearly right and joined up in
/// places nothing was drawn.</summary>
public class SvgPathParsingTests
{
    private static StreamGeometry Read(string data) => new SVGParser().Parse(data);

    private static Vector2 Last(PathFigure figure)
    {
        var segment = figure.Segments[^1];

        return segment switch
        {
            LineSegment line => line.Point,
            CubicBezierSegment cubic => cubic.Point,
            QuadraticBezierSegment quadratic => quadratic.Point,
            ArcSegment arc => arc.Point,
            _ => figure.StartPoint
        };
    }

    // A LOWERCASE COMMAND IS RELATIVE. Read as absolute - which is what upper and lower sharing a branch means - every
    // point after the first landed at the raw number in the file instead of that far along from the pen.
    [Test]
    public void ALowercaseCommandIsRelativeToWhereThePenIs()
    {
        var figure = Read("M 10 10 l 5 0 l 0 5").Figures.Single();

        Assert.That(Last(figure), Is.EqualTo(new Vector2(15, 15)));
    }

    // ONE COMMAND, AS MANY SETS AS IT WAS GIVEN.
    [Test]
    public void ACommandRepeatsForEveryArgumentSetItCarries()
    {
        var figure = Read("M 0 0 L 10 0 20 0 30 0").Figures.Single();

        Assert.Multiple(() =>
        {
            Assert.That(figure.Segments, Has.Count.EqualTo(3), "only the first pair was read");
            Assert.That(Last(figure), Is.EqualTo(new Vector2(30, 0)));
        });
    }

    // ...INCLUDING M, whose extra pairs are lines and NOT new figures.
    [Test]
    public void ExtraPairsAfterAMoveAreLines()
    {
        var figures = Read("M 0 0 10 0 10 10").Figures;

        Assert.Multiple(() =>
        {
            Assert.That(figures, Has.Count.EqualTo(1), "the pairs after M started figures of their own");
            Assert.That(figures[0].Segments, Has.Count.EqualTo(2));
        });
    }

    // EVERY SUB-PATH IS ITS OWN FIGURE. Run together they are joined by a line nobody drew - the stray strokes across
    // an imported icon.
    [Test]
    public void EachSubPathIsAFigureOfItsOwn()
    {
        var figures = Read("M 0 0 L 10 0 Z M 50 50 L 60 50 Z").Figures;

        Assert.Multiple(() =>
        {
            Assert.That(figures, Has.Count.EqualTo(2));
            Assert.That(figures[0].IsClosed, Is.True);
            Assert.That(figures[1].StartPoint, Is.EqualTo(new Vector2(50, 50)));
        });
    }

    // Z PUTS THE PEN BACK at the start of its sub-path, and what follows begins from there.
    [Test]
    public void ClosingPutsThePenBackAtTheStart()
    {
        var figure = Read("M 10 10 L 20 10 L 20 20 Z l 5 0").Figures[^1];

        Assert.That(Last(figure), Is.EqualTo(new Vector2(15, 10)), "the relative move after Z did not start at the start");
    }

    // NUMBERS RUN TOGETHER, which every minifier does.
    [Test]
    public void NumbersRunTogetherAreStillTwoNumbers()
    {
        var figure = Read("M0 0L1.5.5").Figures.Single();

        Assert.That(Last(figure), Is.EqualTo(new Vector2(1.5, 0.5)));
    }

    // ...AND AN EXPONENT'S SIGN BELONGS TO THE EXPONENT. Split on every minus, "1e-5" became two numbers and the whole
    // path came out shifted.
    [Test]
    public void AnExponentIsOneNumber()
    {
        var figure = Read("M 0 0 L 1e2 -1.5e1").Figures.Single();

        Assert.That(Last(figure), Is.EqualTo(new Vector2(100, -15)));
    }

    // A SUB-PATH IS CLOSED FOR THE PURPOSE OF FILLING, whether or not it says Z. Exporters leave Z out freely - the
    // icon this was found on has a dozen sub-paths and not one - and an open contour never reached the winding rule
    // at all, so the shape came out filled inside out: the holes solid and the body hollow.
    [Test]
    public void AnUnclosedSubPathStillFillsAsThoughItWere()
    {
        // A ring with no Z anywhere: outer square one way round, inner square the other.
        var ring = Read("M 0 0 L 100 0 L 100 100 L 0 100 M 25 25 L 25 75 L 75 75 L 75 25");

        ring.FillRule = FillRule.NonZero;
        ring.ProcessGeometry(GeometryType.Both);

        Assert.Multiple(() =>
        {
            Assert.That(Covered(ring, new Vector2(10, 50)), Is.True, "the ring itself is not filled");
            Assert.That(Covered(ring, new Vector2(50, 50)), Is.False, "the hole was filled in");
        });
    }

    // ...AND FILLING IT DOES NOT CLOSE IT. Everything asks for GeometryType.Both, so whatever the fill does to the
    // contours is what the STROKE is drawn from - closing them there puts a phantom edge back to the start, and an
    // icon drawn as three separate lines becomes three triangles. Half the theme's icons, on one line of change.
    [Test]
    public void FillingAnOpenPathLeavesItOpenForTheStroke()
    {
        // The theme's own "snap to edge" icon: three sub-paths, no Z anywhere, meant to be stroked.
        var icon = Read("M1.5,1 L1.5,9 M9,5 L3.4,5 M5.2,3.2 L3.4,5 L5.2,6.8");

        icon.ProcessGeometry(GeometryType.Both);

        foreach (var figure in icon.Figures)
        {
            Assert.That(figure.IsClosed, Is.False, "the fill closed a figure the stroke has to draw open");
        }
    }

    // The grid off a real exported icon: a body with twenty cells cut out of it by running them the other way round,
    // and one small shape crossing the outline - which is what sends the whole thing to the general pipeline.
    private const string Grid =
        "M30.8623 12.6036L27.4694 6.15059L15.097 6C15.0367 6.00021 14.977 6.01368 14.9217 6.03959C14.8664 6.06549 14.81"
        + "67 6.10328 14.7756 6.15059L11.6055 9.76471H11.5747V9.79765L8.30328 13.5294H8.05231V20.1741L7 19.0494V20.4047L8"
        + ".4926 22L9.98521 20.4047V19.0494L8.9329 20.1741V14.4706H23.0224V20.1741L21.9701 19.0494V20.4047L23.4627 22L24."
        + "9553 20.4047V19.0494L23.903 20.1741V14.4706H24.3433L28.9294 9L29.9817 12.7953L28.9294 11.6706V13.0259L30.422 1"
        + "4.6212L29.4043 12.7949V11.6706L30.8623 12.6036ZM18.7559 13.5294L19.5793 12.5882H21.4549L20.6316 13.5294H18.755"
        + "9ZM15.6738 13.5294L16.4972 12.5882H18.3728L17.5495 13.5294H15.6738ZM12.5918 13.5294L13.4151 12.5882H15.2908L14"
        + ".4674 13.5294H12.5918ZM15.0662 10.7059H16.9419L16.1185 11.6471H14.2429L15.0662 10.7059ZM13.0365 11.6471H11.160"
        + "8L11.9841 10.7059H13.8598L13.0365 11.6471ZM20.2441 6.94118L19.4207 7.88235H17.5451L18.3684 6.94118H20.2441ZM23"
        + ".3262 6.94118L22.5028 7.88235H20.6272L21.4505 6.94118H23.3262ZM18.976 9.76471L19.7994 8.82353H21.6751L20.8517 "
        + "9.76471H18.976ZM20.024 10.7059L19.2006 11.6471H17.3249L18.1483 10.7059H20.024ZM18.593 8.82353L17.7696 9.76471H"
        + "15.894L16.7173 8.82353H18.593ZM20.407 11.6471L21.2304 10.7059H23.106L22.2827 11.6471H20.407ZM24.3124 10.7059H2"
        + "6.1881L25.3647 11.6471H23.4891L24.3124 10.7059ZM25.1402 9.76471L25.9635 8.82353H27.8392L26.985 9.79765V9.76471"
        + "H25.1402ZM23.9338 9.76471H22.0581L22.8815 8.82353H24.7571L23.9338 9.76471ZM23.7092 7.88235L24.5326 6.94118H26."
        + "4082L25.5849 7.88235H23.7092ZM15.2864 6.94118H17.162L16.3387 7.88235H14.463L15.2864 6.94118ZM13.6353 8.82353H1"
        + "5.5109L14.6876 9.76471H12.8119L13.6353 8.82353ZM10.333 12.5882H12.2087L11.3853 13.5294H9.50969L10.333 12.5882Z"
        + "M21.838 13.5294L22.6613 12.5882H24.537L23.7136 13.5294H21.838ZM28.5816 8.03294L26.7913 7.88235L27.6147 6.94118"
        + "L27.8392 7.73461L28.5816 8.03294Z";

    // OPEN: the non-zero rule is not applied to contours that genuinely CROSS. This grid's first sub-path crosses its
    // last, which sends it to the general pipeline, where the fill rule is not consulted at all - the contours are
    // merely united and every cell disappears. Closing it means finding the FACES of the cut-up contours (walking the
    // planar graph), which the scanline there does not do. Explicit so it is a standing question and not a silent skip.
    // First, the body of that same icon ON ITS OWN - no cells, nothing crossing. If this is not filled, the trouble is
    // in reading or walking the contour itself and not in the rule.
    [Test]
    public void TheBodyOfARealIconIsFilled()
    {
        var body = Read(Grid[..Grid.IndexOf("ZM")]);

        body.FillRule = FillRule.NonZero;
        body.ProcessGeometry(GeometryType.Both);

        Assert.Multiple(() =>
        {
            Assert.That(Covered(body, new Vector2(20, 12)), Is.True, "the plate itself");
            Assert.That(Covered(body, new Vector2(16, 17)), Is.False, "the room between the arrows is not the shape");
        });
    }

    [Test]
    public void TheCellsOfARealGridAreHoles()
    {
        var grid = Read(Grid);

        grid.FillRule = FillRule.NonZero;
        grid.ProcessGeometry(GeometryType.Both);

        Assert.Multiple(() =>
        {
            Assert.That(Covered(grid, new Vector2(20.1, 13.06)), Is.False, "a cell was filled in");
            Assert.That(Covered(grid, new Vector2(17.0, 13.06)), Is.False, "a cell was filled in");
            Assert.That(Covered(grid, new Vector2(20, 12)), Is.True, "the plate between the cells is not filled");
        });
    }

    private static bool Covered(StreamGeometry geometry, Vector2 point)
    {
        var points = geometry.Mesh.Points;

        for (var i = 0; i + 2 < points.Length; i += 3)
        {
            var a = new Vector2(points[i].X, points[i].Y);
            var b = new Vector2(points[i + 1].X, points[i + 1].Y);
            var c = new Vector2(points[i + 2].X, points[i + 2].Y);

            var d1 = Side(point, a, b);
            var d2 = Side(point, b, c);
            var d3 = Side(point, c, a);

            var negative = d1 < 0 || d2 < 0 || d3 < 0;
            var positive = d1 > 0 || d2 > 0 || d3 > 0;

            if (!(negative && positive)) return true;
        }

        return false;
    }

    private static double Side(Vector2 p, Vector2 a, Vector2 b) =>
        (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);

    // A SMOOTH CURVE is smooth because its first control point is the REFLECTION of the previous one about the current
    // point.
    [Test]
    public void ASmoothCurveReflectsThePreviousControlPoint()
    {
        var figure = Read("M 0 0 C 10 10 20 10 30 0 S 50 -10 60 0").Figures.Single();
        var smooth = (CubicBezierSegment)figure.Segments[^1];

        Assert.That(smooth.ControlPoint1, Is.EqualTo(new Vector2(40, -10)));
    }
}
