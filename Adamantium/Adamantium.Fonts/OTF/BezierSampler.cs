using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.OTF
{
    public static class BezierSampler
    {
        public static SampledOutline[] SampleOutlines(Outline[] outlines, uint rate)
        {
            var sampledOutlines = new List<SampledOutline>();

            foreach (var outline in outlines)
            {
                var sampledPoints = new List<Vector2D>();
                
                foreach (var segment in outline.Segments)
                {
                    var tmp = GeneralSegmentBezier(segment, rate);
                    sampledPoints.AddRange(tmp);
                }
                
                sampledOutlines.Add(new SampledOutline(sampledPoints.ToArray()));
            }

            return sampledOutlines.ToArray();
        }

        private static List<Vector2D> GeneralSegmentBezier(OutlineSegment segment, uint rate)
        {
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
    }
}