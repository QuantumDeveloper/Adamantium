using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    internal static class BezierSampler
    {
        public static SampledOutline[] GenerateOutlines(this Glyph glyph, byte rate)
        {
            if (glyph.OutlineType == OutlineType.TrueType)
            {
                return GenerateQuadraticBezierOutlines(glyph.Outlines, rate);
            }
            else
            {
                return GenerateCubicBezierOutlines(glyph.Outlines, rate);
            }
        }
        
        // Cubic Bezier generator
        private static SampledOutline[] GenerateCubicBezierOutlines(IEnumerable<Outline> outlines, byte rate)
        {
            var sampledOutlines = new List<SampledOutline>();

            foreach (var outline in outlines)
            {
                var sampledPoints = new List<Vector2D>();
                
                foreach (var segment in outline.Segments)
                {
                    var tmp = GenerateCubicOutlineFromSegment(segment, rate);
                    sampledPoints.AddRange(tmp);
                }
                
                sampledOutlines.Add(new SampledOutline(sampledPoints.ToArray()));
            }

            return sampledOutlines.ToArray();
        }

        private static List<Vector2D> GenerateCubicOutlineFromSegment(OutlineSegment segment, byte rate)
        {
            // If this is a line, then just return these points without modification
            if (segment.Points.Count == 2)
            {
                return new List<Vector2D>(segment.Points);
            }

            var sampledPoints = new List<Vector2D>();
            var t = 1.0 / rate;

            for (double d = 0; d <= 1; d += t)
            {
                try
                {
                    var bezierX = Math.Pow(1 - d, 3) * segment.Points[0].X +
                                  3 * d * Math.Pow(1 - d, 2) * segment.Points[1].X +
                                  3 * Math.Pow(d, 2) * (1 - d) * segment.Points[2].X +
                                  Math.Pow(d, 3) * segment.Points[3].X;
                
                    var bezierY = Math.Pow(1 - d, 3) * segment.Points[0].Y +
                                  3 * d * Math.Pow(1 - d, 2) * segment.Points[1].Y +
                                  3 * Math.Pow(d, 2) * (1 - d) * segment.Points[2].Y +
                                  Math.Pow(d, 3) * segment.Points[3].Y;
                
                    sampledPoints.Add(new Vector2D(Math.Round(bezierX, 4), Math.Round(bezierY, 4)));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            return sampledPoints;
        }
        
        // Quadratic Bezier generator
        public static SampledOutline[] GenerateQuadraticBezierOutlines(IEnumerable<Outline> outlines, byte rate)
        {
            var sampledOutlines = new List<SampledOutline>();
            foreach (var outline in outlines)
            {
                var sampledOutline = GenerateQuadraticBezierCurves(outline, rate);
                sampledOutlines.Add(sampledOutline);
            }

            return sampledOutlines.ToArray();
        }
        
        private static SampledOutline GenerateQuadraticBezierCurves(Outline outline, byte rate)
        {
            if (rate == 0)
            {
                rate = 1;
            }

            var step = 1.0 / rate;

            var points = new List<Vector2D>();

            foreach (var segment in outline.Segments)
            {
                // all points from segments are added omitting the [0] point in segment points list, to avoid point duplication in resulting list

                if (segment.Points.Count == 2)
                {
                    // simple line
                    points.Add(segment.Points[1]);
                }
                else
                {
                    // bezier curve
                    for (var t = step; t <= 1; t += step)
                    {
                        points.Add(GetQuadraticCurvePoint(segment.Points[0], segment.Points[1], segment.Points[2], t));
                    }
                }
            }

            // close contour by adding first point to the end of points list
            points.Add(points[0]);

            return new SampledOutline(points.ToArray());
        }

        private static Vector2D GetQuadraticCurvePoint(Vector2D begin, Vector2D control, Vector2D end, double t)
        {
            var x = QuadraticEquation(begin.X, control.X, end.X, t);
            var y = QuadraticEquation(begin.Y, control.Y, end.Y, t);
            
            // Round results because if its double, we will get a lot of digits after point and this will negatively influence on triangulation results
            // 4 digits after point will be enough
            return new Vector2D(Math.Round(x, 4, MidpointRounding.AwayFromZero), Math.Round(y, 4, MidpointRounding.AwayFromZero));
        }
        
        private static double QuadraticEquation(double begin, double control, double end, double t)
        {
            var first = Math.Pow(1 - t, 2);
            var second = 2 * t * (1 - t);
            var third = Math.Pow(t, 2);

            return first * begin + second * control + third * end;
        }
    }
}