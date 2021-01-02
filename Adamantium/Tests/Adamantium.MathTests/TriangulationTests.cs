using System.Collections.Generic;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    public class TriangulationTests
    {
        [Test]
        public void TrinagulateComplexPolygonNonZero()
        {
            List<Vector3F> points = new List<Vector3F>();
            Polygon polygon = new Polygon();
            polygon.FillRule = FillRule.NonZero;
            points.Add(new Vector3F(-10, 0, 0));
            points.Add(new Vector3F(0, 30, 0));
            points.Add(new Vector3F(10, 0, 0));
            points.Add(new Vector3F(-15, 15, 0));
            points.Add(new Vector3F(15, 15, 0));

            polygon.Polygons.Add(new PolygonItem(points));

            var points2 = new List<Vector3F>();

            points2.Add(new Vector3F(-6, 18, 0));
            points2.Add(new Vector3F(2, 18, 0));
            points2.Add(new Vector3F(2, 10, 0));
            points2.Add(new Vector3F(-6, 10, 0));

            polygon.Polygons.Add(new PolygonItem(points2));

            points2.Clear();
            points2.Add(new Vector3F(-2, 22, 0));
            points2.Add(new Vector3F(2, 22, 0));
            points2.Add(new Vector3F(2, 10f, 0));
            points2.Add(new Vector3F(-2, 10f, 0));
            polygon.Polygons.Add(new PolygonItem(points2));

            var triangles = polygon.Fill();

            Assert.AreEqual(84, triangles.Count);

            List<Vector3F> triangulationCheckList = new List<Vector3F>();
            triangulationCheckList.Add(new Vector3F(-15,15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 12, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-10, 12, 0));
            triangulationCheckList.Add(new Vector3F(-10, 0, 0));


            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 2.4f, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 2.4f, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(-6, 18, 0));
            triangulationCheckList.Add(new Vector3F(-5, 18, 0));

            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-6, 18, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-6, 2.4f, 0));
            triangulationCheckList.Add(new Vector3F(-5, 18, 0));
            triangulationCheckList.Add(new Vector3F(-4, 18, 0));
            triangulationCheckList.Add(new Vector3F(-4, 3.6f, 0));
            triangulationCheckList.Add(new Vector3F(-5, 18, 0));
            triangulationCheckList.Add(new Vector3F(-4, 3.6f, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            
            //30-39
            triangulationCheckList.Add(new Vector3F(-4, 18, 0));
            triangulationCheckList.Add(new Vector3F(-2, 24, 0));
            triangulationCheckList.Add(new Vector3F(-2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(-4, 18, 0));
            triangulationCheckList.Add(new Vector3F(-2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(-4, 3.6f, 0));
            triangulationCheckList.Add(new Vector3F(-2, 24, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(0, 22, 0));
            triangulationCheckList.Add(new Vector3F(-2, 24, 0));

            //40-49
            triangulationCheckList.Add(new Vector3F(0, 22, 0));
            triangulationCheckList.Add(new Vector3F(-2, 22, 0));
            triangulationCheckList.Add(new Vector3F(-2, 10, 0));
            triangulationCheckList.Add(new Vector3F(0, 10, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(-2, 10, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(-2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(2, 24, 0));

            //50-59
            triangulationCheckList.Add(new Vector3F(2, 22, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(2, 22, 0));
            triangulationCheckList.Add(new Vector3F(0, 22, 0));
            triangulationCheckList.Add(new Vector3F(0, 10, 0));
            triangulationCheckList.Add(new Vector3F(2, 10, 0));
            triangulationCheckList.Add(new Vector3F(2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(0, 10, 0));
            triangulationCheckList.Add(new Vector3F(2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));

            //60-69
            triangulationCheckList.Add(new Vector3F(2, 24, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(2, 24, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));

            //70-79
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(10, 0, 0));

            //80-84
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(10, 15, 0));
            triangulationCheckList.Add(new Vector3F(15, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TrinagulateComplexPolygonEvenOdd()
        {
            List<Vector3F> points = new List<Vector3F>();
            Polygon polygon = new Polygon();
            points.Add(new Vector3F(-10, 0, 0));
            points.Add(new Vector3F(0, 30, 0));
            points.Add(new Vector3F(10, 0, 0));
            points.Add(new Vector3F(-15, 15, 0));
            points.Add(new Vector3F(15, 15, 0));

            polygon.Polygons.Add(new PolygonItem(points));

            var points2 = new List<Vector3F>();

            points2.Add(new Vector3F(-6, 18, 0));
            points2.Add(new Vector3F(2, 18, 0));
            points2.Add(new Vector3F(2, 10, 0));
            points2.Add(new Vector3F(-6, 10, 0));

            polygon.Polygons.Add(new PolygonItem(points2));

            points2.Clear();
            points2.Add(new Vector3F(-2, 22, 0));
            points2.Add(new Vector3F(2, 22, 0));
            points2.Add(new Vector3F(2, 10f, 0));
            points2.Add(new Vector3F(-2, 10f, 0));
            polygon.Polygons.Add(new PolygonItem(points2));

            var triangles = polygon.Fill();

            Assert.AreEqual(138, triangles.Count);

            List<Vector3F> triangulationCheckList = new List<Vector3F>();

            triangulationCheckList.Add(new Vector3F(-15, 15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 12, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-10, 12, 0));
            triangulationCheckList.Add(new Vector3F(-10, 0, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 12, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 12, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6, 9.6f, 0));
            triangulationCheckList.Add(new Vector3F(-6, 2.4f, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6, 2.4f, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(-6, 18, 0));
            triangulationCheckList.Add(new Vector3F(-5, 18, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 18, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6, 12, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6, 12, 0));
            triangulationCheckList.Add(new Vector3F(-5, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6, 9.6f, 0));
            triangulationCheckList.Add(new Vector3F(-5, 9, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-6, 9.6f, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-6, 2.4f, 0));
            triangulationCheckList.Add(new Vector3F(-5, 18, 0));
            triangulationCheckList.Add(new Vector3F(-4, 18, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(-4, 15, 0));
            triangulationCheckList.Add(new Vector3F(-4, 10, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(-4, 10, 0));
            triangulationCheckList.Add(new Vector3F(-5, 10, 0));
            triangulationCheckList.Add(new Vector3F(-5, 9, 0));
            triangulationCheckList.Add(new Vector3F(-4, 8.4f, 0));
            triangulationCheckList.Add(new Vector3F(-4, 3.6f, 0));
            triangulationCheckList.Add(new Vector3F(-5, 9, 0));
            triangulationCheckList.Add(new Vector3F(-4, 3.6f, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-4, 18, 0));
            triangulationCheckList.Add(new Vector3F(-2, 24, 0));
            triangulationCheckList.Add(new Vector3F(-2, 18, 0));
            triangulationCheckList.Add(new Vector3F(-4, 15, 0));
            triangulationCheckList.Add(new Vector3F(-2, 15, 0));
            triangulationCheckList.Add(new Vector3F(-2, 10, 0));
            triangulationCheckList.Add(new Vector3F(-4, 15, 0));
            triangulationCheckList.Add(new Vector3F(-2, 10, 0));
            triangulationCheckList.Add(new Vector3F(-4, 10, 0));
            triangulationCheckList.Add(new Vector3F(-4, 8.4f, 0));
            triangulationCheckList.Add(new Vector3F(-2, 7.199999f, 0));
            triangulationCheckList.Add(new Vector3F(-2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(-4, 8.4f, 0));
            triangulationCheckList.Add(new Vector3F(-2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(-4, 3.6f, 0));
            triangulationCheckList.Add(new Vector3F(-2, 24, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(0, 22, 0));
            triangulationCheckList.Add(new Vector3F(-2, 24, 0));
            triangulationCheckList.Add(new Vector3F(0, 22, 0));
            triangulationCheckList.Add(new Vector3F(-2, 22, 0));
            triangulationCheckList.Add(new Vector3F(-2, 18, 0));
            triangulationCheckList.Add(new Vector3F(0, 18, 0));
            triangulationCheckList.Add(new Vector3F(0, 15, 0));
            triangulationCheckList.Add(new Vector3F(-2, 18, 0));
            triangulationCheckList.Add(new Vector3F(0, 15, 0));
            triangulationCheckList.Add(new Vector3F(-2, 15, 0));
            triangulationCheckList.Add(new Vector3F(-2, 10, 0));
            triangulationCheckList.Add(new Vector3F(0, 10, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(-2, 10, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(-2, 7.199999f, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(2, 24, 0));
            triangulationCheckList.Add(new Vector3F(2, 22, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(2, 22, 0));
            triangulationCheckList.Add(new Vector3F(0, 22, 0));
            triangulationCheckList.Add(new Vector3F(0, 18, 0));
            triangulationCheckList.Add(new Vector3F(2, 18, 0));
            triangulationCheckList.Add(new Vector3F(2, 15, 0));
            triangulationCheckList.Add(new Vector3F(0, 18, 0));
            triangulationCheckList.Add(new Vector3F(2, 15, 0));
            triangulationCheckList.Add(new Vector3F(0, 15, 0));
            triangulationCheckList.Add(new Vector3F(0, 10, 0));
            triangulationCheckList.Add(new Vector3F(2, 10, 0));
            triangulationCheckList.Add(new Vector3F(2, 7.199999f, 0));
            triangulationCheckList.Add(new Vector3F(0, 10, 0));
            triangulationCheckList.Add(new Vector3F(2, 7.199999f, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(2, 24, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(2, 15, 0));
            triangulationCheckList.Add(new Vector3F(2, 7.199999f, 0));
            triangulationCheckList.Add(new Vector3F(5, 8.999998f, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(2, 7.199999f, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(2, 4.799999f, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(5, 8.999998f, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(5, 8.999998f, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(10, 0, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(10, 15, 0));
            triangulationCheckList.Add(new Vector3F(15, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TrinagulateDoubleStarNonZero()
        {
            List<Vector3F> points = new List<Vector3F>();
            Polygon polygon = new Polygon();
            polygon.FillRule = FillRule.NonZero;
            points.Add(new Vector3F(-10, 0, 0));
            points.Add(new Vector3F(0, 30, 0));
            points.Add(new Vector3F(10, 0, 0));
            points.Add(new Vector3F(-15, 15, 0));
            points.Add(new Vector3F(15, 15, 0));
            points.Add(new Vector3F(-10, 0, 0));
            points.Add(new Vector3F(0, -30, 0));
            points.Add(new Vector3F(10, 0, 0));
            points.Add(new Vector3F(-15, -15, 0));
            points.Add(new Vector3F(15, -15, 0));

            polygon.Polygons.Add(new PolygonItem(points));

            var triangles = polygon.Fill();

            Assert.AreEqual(96, triangles.Count);

            List<Vector3F> triangulationCheckList = new List<Vector3F>();

            triangulationCheckList.Add(new Vector3F(-15, 15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 12, 0));
            triangulationCheckList.Add(new Vector3F(-15, -15, 0));
            triangulationCheckList.Add(new Vector3F(-10, -12, 0));
            triangulationCheckList.Add(new Vector3F(-10, -15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-10, 12, 0));
            triangulationCheckList.Add(new Vector3F(-10, 0, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(-10, 0, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(-10, -12, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(-10, -12, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(-10, -15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(-5, -3, 0));
            triangulationCheckList.Add(new Vector3F(-5, -15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(-5, -15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-5, -3, 0));
            triangulationCheckList.Add(new Vector3F(0, -6, 0));
            triangulationCheckList.Add(new Vector3F(0, -30, 0));
            triangulationCheckList.Add(new Vector3F(-5, -3, 0));
            triangulationCheckList.Add(new Vector3F(0, -30, 0));
            triangulationCheckList.Add(new Vector3F(-5, -15, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(0, -6, 0));
            triangulationCheckList.Add(new Vector3F(5, -3, 0));
            triangulationCheckList.Add(new Vector3F(5, -15, 0));
            triangulationCheckList.Add(new Vector3F(0, -6, 0));
            triangulationCheckList.Add(new Vector3F(5, -15, 0));
            triangulationCheckList.Add(new Vector3F(0, -30, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(5, -3, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(5, -3, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(5, -15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(10, 0, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(10, 0, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -9.999996f, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(10, -12, 0));
            triangulationCheckList.Add(new Vector3F(10, -15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(10, -15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(10, 15, 0));
            triangulationCheckList.Add(new Vector3F(15, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(10, -12, 0));
            triangulationCheckList.Add(new Vector3F(15, -15, 0));
            triangulationCheckList.Add(new Vector3F(10, -15, 0));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TrinagulateDoubleStarEvenOdd()
        {
            List<Vector3F> points = new List<Vector3F>();
            Polygon polygon = new Polygon();
            points.Add(new Vector3F(-10, 0, 0));
            points.Add(new Vector3F(0, 30, 0));
            points.Add(new Vector3F(10, 0, 0));
            points.Add(new Vector3F(-15, 15, 0));
            points.Add(new Vector3F(15, 15, 0));
            points.Add(new Vector3F(-10, 0, 0));
            points.Add(new Vector3F(0, -30, 0));
            points.Add(new Vector3F(10, 0, 0));
            points.Add(new Vector3F(-15, -15, 0));
            points.Add(new Vector3F(15, -15, 0));

            polygon.Polygons.Add(new PolygonItem(points));

            var triangles = polygon.Fill();

            Assert.AreEqual(108, triangles.Count);

            List<Vector3F> triangulationCheckList = new List<Vector3F>();

            triangulationCheckList.Add(new Vector3F(-15, 15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 12, 0));
            triangulationCheckList.Add(new Vector3F(-15, -15, 0));
            triangulationCheckList.Add(new Vector3F(-10, -12, 0));
            triangulationCheckList.Add(new Vector3F(-10, -15, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-10, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-10, 12, 0));
            triangulationCheckList.Add(new Vector3F(-10, 0, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(-10, 0, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(-10, -12, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(-10, -12, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(-10, -15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-5, 9, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(-5, -3, 0));
            triangulationCheckList.Add(new Vector3F(-5, -9, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(-5, -9, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(-5, -15, 0));
            triangulationCheckList.Add(new Vector3F(-6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 15, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(0, 15, 0));
            triangulationCheckList.Add(new Vector3F(-5, 9, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(-5, 3, 0));
            triangulationCheckList.Add(new Vector3F(-5, -3, 0));
            triangulationCheckList.Add(new Vector3F(0, -6, 0));
            triangulationCheckList.Add(new Vector3F(-5, -9, 0));
            triangulationCheckList.Add(new Vector3F(-5, -15, 0));
            triangulationCheckList.Add(new Vector3F(0, -15, 0));
            triangulationCheckList.Add(new Vector3F(0, -30, 0));
            triangulationCheckList.Add(new Vector3F(0, 30, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(0, 15, 0));
            triangulationCheckList.Add(new Vector3F(0, 6, 0));
            triangulationCheckList.Add(new Vector3F(5, 8.999998f, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(0, -6, 0));
            triangulationCheckList.Add(new Vector3F(5, -3, 0));
            triangulationCheckList.Add(new Vector3F(5, -9.000004f, 0));
            triangulationCheckList.Add(new Vector3F(0, -15, 0));
            triangulationCheckList.Add(new Vector3F(5, -15, 0));
            triangulationCheckList.Add(new Vector3F(0, -30, 0));
            triangulationCheckList.Add(new Vector3F(5, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(5, 8.999998f, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(5, 8.999998f, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(5, 3, 0));
            triangulationCheckList.Add(new Vector3F(5, -3, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(5, -3, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(5, -9.000004f, 0));
            triangulationCheckList.Add(new Vector3F(5, -15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 10, 0));
            triangulationCheckList.Add(new Vector3F(10, 0, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, 2, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -2, 0));
            triangulationCheckList.Add(new Vector3F(10, 0, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(10, -12, 0));
            triangulationCheckList.Add(new Vector3F(10, -15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -10, 0));
            triangulationCheckList.Add(new Vector3F(10, -15, 0));
            triangulationCheckList.Add(new Vector3F(6.666667f, -15, 0));
            triangulationCheckList.Add(new Vector3F(10, 15, 0));
            triangulationCheckList.Add(new Vector3F(15, 15, 0));
            triangulationCheckList.Add(new Vector3F(10, 12, 0));
            triangulationCheckList.Add(new Vector3F(10, -12, 0));
            triangulationCheckList.Add(new Vector3F(15, -15, 0));
            triangulationCheckList.Add(new Vector3F(10, -15, 0));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }
    }
}
