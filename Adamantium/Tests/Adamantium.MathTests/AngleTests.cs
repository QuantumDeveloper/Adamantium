using System;
using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    public class AngleTests
    {
        [Test]
        public void AngleTest()
        {
            var prev = new LineSegment2D(new Vector2D(1, 10), new Vector2D(1, 1));
            var cur = new LineSegment2D(new Vector2D(1, 10), new Vector2D(2, 20));

            var res = MathHelper.DetermineAngleInDegrees(prev.Start, prev.End, cur.Start, cur.End);
        }

        [Test]
        public void PointToLineDistanceTest()
        {
            var line = new LineSegment2D(new Vector2D(1, 1), new Vector2D(10, 1.1));
            var point = new Vector2D(-20,4);

            var res = MathHelper.PointToLineDistance(line.Start, line.End, point);
        }
    }
}