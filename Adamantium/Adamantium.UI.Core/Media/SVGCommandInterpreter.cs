using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

internal class SVGCommandInterpreter
{
    /// <summary>Reads a path's commands into figures.
    /// <para>Three rules SVG states and this used to miss, each of which quietly bends a drawing rather than breaking
    /// it: a LOWERCASE command is relative to where the pen is; one command carries AS MANY argument sets as it was
    /// given ("L 1 1 2 2" is two lines, and "M" after its first pair continues as lineto); and Z puts the pen back at
    /// the start of its sub-path, which is where anything after it begins.</para></summary>
    public StreamGeometry InterpretCommands(List<SVGCommand> commands)
    {
        var current = Vector2.Zero;
        var start = Vector2.Zero;
        var geometry = new StreamGeometry();
        var context = geometry.Open();
        IFigureSegments figure = null;

        foreach (var command in commands)
        {
            var relative = char.IsLower(command.Command);
            var letter = char.ToUpperInvariant(command.Command);
            var args = command.Arguments;
            var at = 0;

            if (letter == 'Z')
            {
                figure?.CloseFigure();
                current = start;
                continue;
            }

            var step = letter switch
            {
                'M' or 'L' or 'T' => 2,
                'H' or 'V' => 1,
                'C' => 6,
                'S' or 'Q' => 4,
                'A' => 7,
                _ => 0
            };

            if (step == 0) continue;

            var first = true;

            while (at + step <= args.Count)
            {
                switch (letter)
                {
                    case 'M':
                    {
                        var point = Point(args, ref at, relative, current);

                        // Only the FIRST pair begins a figure; the rest are lines, which is what SVG says and what
                        // every exporter relies on.
                        if (first)
                        {
                            figure = context.BeginFigure(point, true, false);
                            start = point;
                        }
                        else
                        {
                            figure?.LineTo(point);
                        }

                        current = point;
                        break;
                    }

                    case 'L':
                    {
                        current = Point(args, ref at, relative, current);
                        figure?.LineTo(current);
                        break;
                    }

                    case 'H':
                    {
                        current = new Vector2(relative ? current.X + args[at++] : args[at++], current.Y);
                        figure?.LineTo(current);
                        break;
                    }

                    case 'V':
                    {
                        current = new Vector2(current.X, relative ? current.Y + args[at++] : args[at++]);
                        figure?.LineTo(current);
                        break;
                    }

                    case 'C':
                    {
                        var one = Point(args, ref at, relative, current);
                        var two = Point(args, ref at, relative, current);
                        var end = Point(args, ref at, relative, current);

                        figure?.CubicBezierTo(one, two, end);
                        _lastCubicControl = two;
                        _lastQuadraticControl = null;
                        current = end;
                        break;
                    }

                    case 'S':
                    {
                        var two = Point(args, ref at, relative, current);
                        var end = Point(args, ref at, relative, current);

                        // The reflection of the previous curve's second control point about the current point - that
                        // is what makes a smooth curve smooth. Reflected about a DIRECTION instead, it landed
                        // somewhere unrelated and the curve kinked at every join.
                        var one = _lastCubicControl is { } had
                            ? new Vector2(2 * current.X - had.X, 2 * current.Y - had.Y)
                            : current;

                        figure?.CubicBezierTo(one, two, end);
                        _lastCubicControl = two;
                        _lastQuadraticControl = null;
                        current = end;
                        break;
                    }

                    case 'Q':
                    {
                        var control = Point(args, ref at, relative, current);
                        var end = Point(args, ref at, relative, current);

                        figure?.QuadraticBezierTo(control, end);
                        _lastQuadraticControl = control;
                        _lastCubicControl = null;
                        current = end;
                        break;
                    }

                    case 'T':
                    {
                        var end = Point(args, ref at, relative, current);
                        var control = _lastQuadraticControl is { } had
                            ? new Vector2(2 * current.X - had.X, 2 * current.Y - had.Y)
                            : current;

                        figure?.QuadraticBezierTo(control, end);
                        _lastQuadraticControl = control;
                        _lastCubicControl = null;
                        current = end;
                        break;
                    }

                    case 'A':
                    {
                        var size = new Size(args[at++], args[at++]);
                        var angle = args[at++];
                        var large = args[at++] != 0;
                        var sweep = (SweepDirection)(int)args[at++];
                        var end = Point(args, ref at, relative, current);

                        figure?.ArcTo(end, size, angle, large, sweep, true);
                        current = end;
                        break;
                    }
                }

                if (letter is not ('C' or 'S')) _lastCubicControl = null;
                if (letter is not ('Q' or 'T')) _lastQuadraticControl = null;

                first = false;
            }
        }

        return geometry;
    }

    private Vector2? _lastCubicControl;
    private Vector2? _lastQuadraticControl;

    private static Vector2 Point(List<double> args, ref int at, bool relative, Vector2 from)
    {
        var x = args[at++];
        var y = args[at++];

        return relative ? new Vector2(from.X + x, from.Y + y) : new Vector2(x, y);
    }
}
