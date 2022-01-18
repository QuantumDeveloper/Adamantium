using System;
using System.Collections.Generic;

namespace Adamantium.UI.Media;

internal class SVGCommandInterpreter
{
    public PathGeometry InterpretCommands(List<SVGCommand> commands)
    {
        var pathGeometry = new PathGeometry();
        pathGeometry.Figures = new PathFigureCollection();
        PathFigure currentFigure = null;
        Vector2 currentPoint = Vector2.Zero;

        foreach (var command in commands)
        {
            int index = 0;
            switch (command.Command)
            {
                case 'M':
                case 'm':
                    currentFigure = new PathFigure();
                    currentFigure.Segments = new PathSegmentCollection();
                    currentFigure.StartPoint = new Vector2(command.Arguments[0], command.Arguments[1]);
                    pathGeometry.Figures.Add(currentFigure);
                    currentPoint = currentFigure.StartPoint;
                    break;
                case 'A':
                case 'a':
                    var arcSegment = new ArcSegment();
                    currentFigure.Segments.Add(arcSegment);
                    arcSegment.Size = new Size(command.Arguments[index++], command.Arguments[index++]);
                    arcSegment.RotationAngle = command.Arguments[index++];
                    arcSegment.IsLargeArc = Convert.ToBoolean(command.Arguments[index++]);
                    arcSegment.SweepDirection = (SweepDirection)command.Arguments[index++];
                    arcSegment.Point = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    currentPoint = arcSegment.Point;
                    break;
                case 'L':
                case 'l':
                    var lineSegment = new LineSegment();
                    currentFigure.Segments.Add(lineSegment);
                    lineSegment.Point = new Vector2(command.Arguments[0], command.Arguments[1]);
                    currentPoint = lineSegment.Point;
                    break;
                case 'H':
                case 'h':
                    var horizontalSegment = new LineSegment();
                    currentFigure.Segments.Add(horizontalSegment);
                    horizontalSegment.Point = new Vector2(command.Arguments[0], currentPoint.Y);
                    currentPoint = horizontalSegment.Point;
                    break;
                case 'V':
                case 'v':
                    var verticalSegment = new LineSegment();
                    currentFigure.Segments.Add(verticalSegment);
                    verticalSegment.Point = new Vector2(currentPoint.X, command.Arguments[0]);
                    currentPoint = verticalSegment.Point;
                    break;
                case 'C':
                case 'c':
                {
                    var cubicBezier = new CubicBezierSegment();
                    currentFigure.Segments.Add(cubicBezier);
                    cubicBezier.ControlPoint1 = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    cubicBezier.ControlPoint2 = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    cubicBezier.Point = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    currentPoint = cubicBezier.Point;
                    break;
                }
                case 'Q':
                case 'q':
                {
                    var quadraticBezier = new QuadraticBezierSegment();
                    currentFigure.Segments.Add(quadraticBezier);
                    quadraticBezier.ControlPoint = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    quadraticBezier.Point = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    currentPoint = quadraticBezier.Point;
                    break;
                }
                case 'S':
                case 's':
                {
                    var cubicBezier = new CubicBezierSegment();
                    currentFigure.Segments.Add(cubicBezier);
                    cubicBezier.ControlPoint2 = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    cubicBezier.Point = new Vector2(command.Arguments[index++], command.Arguments[index++]);

                    if (currentFigure.Segments.Count == 0)
                    {
                        cubicBezier.ControlPoint1 = currentPoint;
                    }
                    else
                    {
                        var prevSegment = currentFigure.Segments[^2];
                        if (prevSegment is CubicBezierSegment cubic)
                        {
                            var direction = cubicBezier.Point - currentPoint;
                            direction.Normalize();
                            var controlPoint = Vector2.Reflect(cubic.ControlPoint2, direction);
                            cubicBezier.ControlPoint1 = controlPoint;
                        }
                        else
                        {
                            cubicBezier.ControlPoint1 = currentPoint;
                        }

                        currentPoint = cubicBezier.Point;
                    }
                    break;
                }
                case 'T':
                case 't':
                {
                    var quadraticBezier = new QuadraticBezierSegment();
                    currentFigure.Segments.Add(quadraticBezier);
                    quadraticBezier.Point = new Vector2(command.Arguments[index++], command.Arguments[index++]);

                    if (currentFigure.Segments.Count == 0)
                    {
                        quadraticBezier.ControlPoint = currentPoint;
                    }
                    else
                    {
                        var prevSegment = currentFigure.Segments[^2];
                        if (prevSegment is QuadraticBezierSegment quadratic)
                        {
                            var direction = quadraticBezier.Point - currentPoint;
                            direction.Normalize();
                            var controlPoint = Vector2.Reflect(quadratic.ControlPoint, direction);
                            quadraticBezier.ControlPoint = controlPoint;
                        }
                        else
                        {
                            quadraticBezier.ControlPoint = currentPoint;
                        }

                        currentPoint = quadraticBezier.Point;
                    }

                    break;
                }
            }
        }

        return pathGeometry;
    }
}