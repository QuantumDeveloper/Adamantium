using System;
using Adamantium.Fonts.DataOut;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    internal static class Bezier
    {
        public static GlyphContour GenerateContour(Contour contour, float step)
        {
            // if TTF
            return GenerateQuadraticBezierCurves(contour, step);
            // if OTF
            // return GenerateCubicBezierCurves(contour, step);
        }

        private static GlyphContour GenerateQuadraticBezierCurves(Contour contour, float step)
        {
            if (step <= 0 || step > 1)
            {
                throw new ParserException("step cannot be <= 0 and > 1");
            }

            GlyphContour glyphContour = new GlyphContour();

            foreach (ContourSegment segment in contour.Segments)
            {
                // all points from segments are added omitting the [0] point in segment points list, to avoid point duplication in resulting list

                if (segment.Points.Count == 2)
                {
                    // simple line
                    glyphContour.Points.Add(segment.Points[1]);
                }
                else
                {
                    // bezier curve
                    for (float t = step; t <= 1; t += step)
                    {
                        glyphContour.Points.Add(GetQuadraticCurvePoint(segment.Points[0], segment.Points[1], segment.Points[2], t));
                    }
                }
            }

            // close contour by adding first point to the end of points list
            glyphContour.Points.Add(glyphContour.Points[0]);

            return glyphContour;
        }

        private static GlyphContour GenerateCubicBezierCurves(Contour contour, float step)
        {
            throw new NotImplementedException();
        }

        private static Vector2D GetQuadraticCurvePoint(Vector2D begin, Vector2D control, Vector2D end, float t)
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

        private static float QuadraticEquation(float begin, float control, float end, float t)
        {
            float first = (float)Math.Pow(1 - t, 2);
            float second = 2 * t * (1 - t);
            float third = (float)Math.Pow(t, 2);

            return first * begin + second * control + third * end;
        }
    }
}
