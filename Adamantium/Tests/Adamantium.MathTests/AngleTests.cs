﻿using System;
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
            var start = new Vector2D(10, 0);
            var end = new Vector2D(10, 0);

            var cross = MathHelper.Cross2D(start, end);
        }

        [Test]
        public void PointToLineDistanceTest()
        {
            var line = new LineSegment2D(new Vector2D(1, 1), new Vector2D(10, 1.1));
            var point = new Vector2D(-20,4);

            var res = MathHelper.PointToLineDistance(line.Start, line.End, point);
        }

        [Test]
        public void EllipseAngleTest()
        {
            var rectangleSector = 90F;
            var tessellation = 20;
            var startAngle = -180;

            var center = new Vector2D(0);
            var radius = new Vector2D(10);
            var vertices = new List<Vector2D>();

            var angleItem = -MathHelper.DegreesToRadians(rectangleSector / tessellation);
            var angle = MathHelper.DegreesToRadians(startAngle);
            for (int i = 0; i < tessellation; ++i)
            {
                var x = center.X + (radius.X * (float) Math.Cos(angle));
                var y = center.Y + (radius.Y * (float) Math.Sin(angle));
                angle += angleItem;
                vertices.Add(new Vector2D(x, y));
            }
        }
    }
}