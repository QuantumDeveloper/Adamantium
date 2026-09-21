using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

public class SVGParser
{
    private string pattern = @"(?=[MZLHVCSQTAmzlhvcsqta])";

    public List<SVGCommand> Commands { get; private set; }

    public StreamGeometry Parse(string svgString)
    {
        svgString = svgString?.TrimStart() ?? string.Empty;

        // Optional leading fill-rule token (XAML/WPF path mini-language): "F0" = EvenOdd, "F1" = NonZero.
        FillRule? fillRule = null;
        if (svgString.Length >= 2 && (svgString[0] == 'F' || svgString[0] == 'f'))
        {
            if (svgString[1] == '0') fillRule = FillRule.EvenOdd;
            else if (svgString[1] == '1') fillRule = FillRule.NonZero;
            if (fillRule != null) svgString = svgString.Substring(2).TrimStart();
        }

        var tokens = Regex.Split(svgString, pattern).Where(t => !string.IsNullOrEmpty(t));
        Commands = new List<SVGCommand>();
        foreach (var token in tokens)
        {
            Commands.Add(SVGCommand.Parse(token));
        }

        if (Commands.Count == 0) return new StreamGeometry();

        var geometry = new SVGCommandInterpreter().InterpretCommands(Commands);

        if (fillRule != null) geometry.FillRule = fillRule.Value;

        return geometry;
    }

    /// <summary>The geometry said BACK as SVG path data - the other half of <see cref="Parse"/>, and here beside it so
    /// that reading a path and writing one stay one statement of the same grammar.
    /// <para>EVERY FIGURE STARTS WITH ITS OWN M. That is what keeps two sub-paths two: run together, the end of one is
    /// joined to the start of the next by a line nobody drew, which is the web of stray strokes an imported icon comes
    /// in wearing.</para></summary>
    public static string ToPathData(StreamGeometry geometry)
    {
        if (geometry == null) return string.Empty;

        var text = new StringBuilder();

        foreach (var figure in geometry.Figures)
        {
            if (figure == null) continue;

            if (text.Length > 0) text.Append(' ');

            text.Append('M').Append(' ').Append(Pair(figure.StartPoint));

            foreach (var segment in figure.Segments ?? new PathSegmentCollection()) Write(text, segment);

            if (figure.IsClosed) text.Append(" Z");
        }

        return text.ToString();
    }

    private static void Write(StringBuilder text, PathSegment segment)
    {
        switch (segment)
        {
            case LineSegment line:
                text.Append(" L ").Append(Pair(line.Point));
                break;

            case ArcSegment arc:
                text.Append(" A ")
                    .Append(Num(arc.Size.Width)).Append(' ').Append(Num(arc.Size.Height)).Append(' ')
                    .Append(Num(arc.RotationAngle)).Append(' ')
                    .Append(arc.IsLargeArc ? '1' : '0').Append(' ')
                    .Append(arc.SweepDirection == SweepDirection.Clockwise ? '1' : '0').Append(' ')
                    .Append(Pair(arc.Point));
                break;

            case CubicBezierSegment cubic:
                text.Append(" C ").Append(Pair(cubic.ControlPoint1)).Append(' ')
                    .Append(Pair(cubic.ControlPoint2)).Append(' ').Append(Pair(cubic.Point));
                break;

            case QuadraticBezierSegment quadratic:
                text.Append(" Q ").Append(Pair(quadratic.ControlPoint)).Append(' ').Append(Pair(quadratic.Point));
                break;

            // A spline (BSplineSegment, and NurbsSegment under it) is a polyline as far as SVG is concerned: its walked
            // points are sayable, the curve through them is not, and said as curves it would come back a different
            // shape.
            case PolylineSegment polyline:
                foreach (var point in polyline.Points) text.Append(" L ").Append(Pair(point));
                break;

            case PolyCubicBezierSegment poly:
            {
                var points = poly.Points;

                for (var i = 0; i + 2 < points.Count; i += 3)
                {
                    text.Append(" C ").Append(Pair(points[i])).Append(' ')
                        .Append(Pair(points[i + 1])).Append(' ').Append(Pair(points[i + 2]));
                }

                break;
            }

            case PolyQuadraticBezierSegment poly:
            {
                var points = poly.Points;

                for (var i = 0; i + 1 < points.Count; i += 2)
                {
                    text.Append(" Q ").Append(Pair(points[i])).Append(' ').Append(Pair(points[i + 1]));
                }

                break;
            }

        }
    }

    private static string Pair(Vector2 point) => $"{Num(point.X)} {Num(point.Y)}";

    private static string Num(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public override string ToString()
    {
        return $"Commands count: {Commands.Count} ";
    }
}