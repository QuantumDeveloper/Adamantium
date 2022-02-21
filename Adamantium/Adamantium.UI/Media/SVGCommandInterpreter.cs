using System;
using System.Collections.Generic;

namespace Adamantium.UI.Media;

internal class SVGCommandInterpreter
{
    public StreamGeometry InterpretCommands(List<SVGCommand> commands)
    {
        Vector2 currentPoint = Vector2.Zero;
        var streamGeometry = new StreamGeometry();
        var context = streamGeometry.Open();
        IFigureSegments figureSegments = null;

        foreach (var command in commands)
        {
            int index = 0;
            switch (command.Command)
            {
                case 'M':
                case 'm':
                    var startPoint = new Vector2(command.Arguments[0], command.Arguments[1]);
                    figureSegments = context.BeginFigure(startPoint, true, true);
                    currentPoint = startPoint;
                    break;
                case 'A':
                case 'a':
                {
                    var size = new Size(command.Arguments[index++], command.Arguments[index++]);
                    var rotationAngle = command.Arguments[index++];
                    var isLargeArc = Convert.ToBoolean(command.Arguments[index++]);
                    var sweepDirection = (SweepDirection)command.Arguments[index++];
                    var point = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    figureSegments?.ArcTo(point, size, rotationAngle, isLargeArc, sweepDirection, true);
                    currentPoint = point;
                    break;
                }
                case 'L':
                case 'l':
                    currentPoint = new Vector2(command.Arguments[0], command.Arguments[1]);
                    figureSegments?.LineTo(currentPoint);
                    break;
                case 'H':
                case 'h':
                    currentPoint = new Vector2(command.Arguments[0], currentPoint.Y);
                    figureSegments?.LineTo(currentPoint);
                    break;
                case 'V':
                case 'v':
                    currentPoint = new Vector2(currentPoint.X, command.Arguments[0]);
                    figureSegments?.LineTo(currentPoint);
                    break;
                case 'C':
                case 'c':
                {
                    var controlPoint1 = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    var controlPoint2 = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    var point = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    figureSegments?.CubicBezierTo(controlPoint1, controlPoint2, point);
                    currentPoint = point;
                    break;
                }
                case 'Q':
                case 'q':
                {
                    var controlPoint = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    var point = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    currentPoint = point;
                    figureSegments?.QuadraticBezierTo(controlPoint, point);
                    break;
                }
                case 'S':
                case 's':
                {
                    Vector2 controlPoint1;
                    var controlPoint2 = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    var point = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    
                    if (context.SegmentsCount == 0)
                    {
                        controlPoint1 = currentPoint;
                    }
                    else
                    {
                        var prevSegment = context.GetLastSegment();
                        if (prevSegment is CubicBezierSegment cubic)
                        {
                            var direction = point - currentPoint;
                            direction.Normalize();
                            controlPoint1 = Vector2.Reflect(cubic.ControlPoint2, direction);
                        }
                        else
                        {
                            controlPoint1 = currentPoint;
                        }

                        currentPoint = point;
                    }

                    figureSegments?.CubicBezierTo(controlPoint1, controlPoint2, point);
                    break;
                }
                case 'T':
                case 't':
                {
                    Vector2 controlPoint;
                    var point = new Vector2(command.Arguments[index++], command.Arguments[index++]);
                    
                    if (context.SegmentsCount == 0)
                    {
                        controlPoint = currentPoint;
                    }
                    else
                    {
                        var prevSegment = context.GetLastSegment();
                        if (prevSegment is QuadraticBezierSegment quadratic)
                        {
                            var direction = point - currentPoint;
                            direction.Normalize();
                            controlPoint = Vector2.Reflect(quadratic.ControlPoint, direction);
                        }
                        else
                        {
                            controlPoint = currentPoint;
                        }

                        currentPoint = point;
                    }

                    figureSegments?.QuadraticBezierTo(controlPoint, point);
                    break;
                }
            }
        }

        return streamGeometry;
    }
}