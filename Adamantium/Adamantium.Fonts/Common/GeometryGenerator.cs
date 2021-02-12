using System;
using Adamantium.Fonts.DataOut;

namespace Adamantium.Fonts.Common
{
    static internal class GeometryGenerator
    {
        static GeometryGenerator()
        {
        }

        static public GlyphContour GenerateContour(Contour contour, float step)
        {
            // if TTF
            return GenerateQuadraticBezierCurves(contour, step);
            // if OTF
            // return GenerateCubicBezierCurves(contour, step);
        }

        static private GlyphContour GenerateQuadraticBezierCurves(Contour contour, float step)
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

        static private GlyphContour GenerateCubicBezierCurves(Contour contour, float step)
        {
            throw new NotImplementedException();
        }

        static private Point GetQuadraticCurvePoint(Point begin, Point control, Point end, float t)
        {
            float x = QuadraticEquation(begin.X, control.X, end.X, t);
            float y = QuadraticEquation(begin.Y, control.Y, end.Y, t);

            return new Point(x, y);
        }

        static private float QuadraticEquation(float begin, float control, float end, float t)
        {
            float first = (float)Math.Pow(1 - t, 2);
            float second = 2 * t * (1 - t);
            float third = (float)Math.Pow(t, 2);

            return first * begin + second * control + third * end;
        }
    }
}
