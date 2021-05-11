using System.Collections.Generic;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    public class TriangulationTests
    {
        [Test]
        public void TriangulateComplexPolygonNonZero()
        {
            var points = new List<Vector2D>();
            Polygon polygon = new Polygon();
            polygon.FillRule = FillRule.NonZero;
            points.Add(new Vector2D(-10));
            points.Add(new Vector2D(0, 30));
            points.Add(new Vector2D(10));
            points.Add(new Vector2D(-15, 15));
            points.Add(new Vector2D(15, 15));

            polygon.Polygons.Add(new PolygonItem(points));

            var points2 = new List<Vector2D>();

            points2.Add(new Vector2D(-6, 18));
            points2.Add(new Vector2D(2, 18));
            points2.Add(new Vector2D(2, 10));
            points2.Add(new Vector2D(-6, 10));

            polygon.Polygons.Add(new PolygonItem(points2));

            points2.Clear();
            points2.Add(new Vector2D(-2, 22));
            points2.Add(new Vector2D(2, 22));
            points2.Add(new Vector2D(2, 10));
            points2.Add(new Vector2D(-2, 10));
            polygon.Polygons.Add(new PolygonItem(points2));

            var triangles = polygon.Fill();

            Assert.AreEqual(84, triangles.Count);

            var triangulationCheckList = new List<Vector2D>();
            triangulationCheckList.Add(new Vector2D(-15,15));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-10, 12));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667, 10));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667, 10));
            triangulationCheckList.Add(new Vector2D(-10, 12));
            triangulationCheckList.Add(new Vector2D(-10));


            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-6, 15));
            triangulationCheckList.Add(new Vector2D(-6, 2.4));
            triangulationCheckList.Add(new Vector2D(-6.666667, 15));
            triangulationCheckList.Add(new Vector2D(-6, 2.4));
            triangulationCheckList.Add(new Vector2D(-6.666667, 2));
            triangulationCheckList.Add(new Vector2D(-6, 18));
            triangulationCheckList.Add(new Vector2D(-5, 18));

            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-6, 18));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-6, 2.4));
            triangulationCheckList.Add(new Vector2D(-5, 18));
            triangulationCheckList.Add(new Vector2D(-4, 18));
            triangulationCheckList.Add(new Vector2D(-4, 3.6));
            triangulationCheckList.Add(new Vector2D(-5, 18));
            triangulationCheckList.Add(new Vector2D(-4, 3.6));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            
            //30-39
            triangulationCheckList.Add(new Vector2D(-4, 18));
            triangulationCheckList.Add(new Vector2D(-2, 24));
            triangulationCheckList.Add(new Vector2D(-2, 4.799999));
            triangulationCheckList.Add(new Vector2D(-4, 18));
            triangulationCheckList.Add(new Vector2D(-2, 4.799999));
            triangulationCheckList.Add(new Vector2D(-4, 3.6));
            triangulationCheckList.Add(new Vector2D(-2, 24));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(0, 22));
            triangulationCheckList.Add(new Vector2D(-2, 24));

            //40-49
            triangulationCheckList.Add(new Vector2D(0, 22));
            triangulationCheckList.Add(new Vector2D(-2, 22));
            triangulationCheckList.Add(new Vector2D(-2, 10));
            triangulationCheckList.Add(new Vector2D(0, 10));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(-2, 10));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(-2, 4.799999f));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(2, 24));

            //50-59
            triangulationCheckList.Add(new Vector2D(2, 22));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(2, 22));
            triangulationCheckList.Add(new Vector2D(0, 22));
            triangulationCheckList.Add(new Vector2D(0, 10));
            triangulationCheckList.Add(new Vector2D(2, 10));
            triangulationCheckList.Add(new Vector2D(2, 4.799999f));
            triangulationCheckList.Add(new Vector2D(0, 10));
            triangulationCheckList.Add(new Vector2D(2, 4.799999f));
            triangulationCheckList.Add(new Vector2D(0, 6));

            //60-69
            triangulationCheckList.Add(new Vector2D(2, 24));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(2, 24));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(2, 4.799999f));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(5, 15));

            //70-79
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(10, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(10));

            //80-84
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(10, 15));
            triangulationCheckList.Add(new Vector2D(15, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TriangulateComplexPolygonEvenOdd()
        {
            List<Vector2D> points = new List<Vector2D>();
            Polygon polygon = new Polygon();
            points.Add(new Vector2D(-10));
            points.Add(new Vector2D(0, 30));
            points.Add(new Vector2D(10));
            points.Add(new Vector2D(-15, 15));
            points.Add(new Vector2D(15, 15));

            polygon.Polygons.Add(new PolygonItem(points));

            var points2 = new List<Vector2D>();

            points2.Add(new Vector2D(-6, 18));
            points2.Add(new Vector2D(2, 18));
            points2.Add(new Vector2D(2, 10));
            points2.Add(new Vector2D(-6, 10));

            polygon.Polygons.Add(new PolygonItem(points2));

            points2.Clear();
            points2.Add(new Vector2D(-2, 22));
            points2.Add(new Vector2D(2, 22));
            points2.Add(new Vector2D(2, 10f));
            points2.Add(new Vector2D(-2, 10f));
            polygon.Polygons.Add(new PolygonItem(points2));

            var triangles = polygon.Fill();

            Assert.AreEqual(138, triangles.Count);

            List<Vector2D> triangulationCheckList = new List<Vector2D>();

            triangulationCheckList.Add(new Vector2D(-15, 15));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-10, 12));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-10, 12));
            triangulationCheckList.Add(new Vector2D(-10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-6, 15));
            triangulationCheckList.Add(new Vector2D(-6, 12));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-6, 12));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-6, 9.6f));
            triangulationCheckList.Add(new Vector2D(-6, 2.4f));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-6, 2.4f));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(-6, 18));
            triangulationCheckList.Add(new Vector2D(-5, 18));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(-6, 18));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(-6, 15));
            triangulationCheckList.Add(new Vector2D(-6, 12));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(-5, 10));
            triangulationCheckList.Add(new Vector2D(-6, 12));
            triangulationCheckList.Add(new Vector2D(-5, 10));
            triangulationCheckList.Add(new Vector2D(-6, 10));
            triangulationCheckList.Add(new Vector2D(-6, 9.6f));
            triangulationCheckList.Add(new Vector2D(-5, 9));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-6, 9.6f));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-6, 2.4f));
            triangulationCheckList.Add(new Vector2D(-5, 18));
            triangulationCheckList.Add(new Vector2D(-4, 18));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(-4, 15));
            triangulationCheckList.Add(new Vector2D(-4, 10));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(-4, 10));
            triangulationCheckList.Add(new Vector2D(-5, 10));
            triangulationCheckList.Add(new Vector2D(-5, 9));
            triangulationCheckList.Add(new Vector2D(-4, 8.4f));
            triangulationCheckList.Add(new Vector2D(-4, 3.6f));
            triangulationCheckList.Add(new Vector2D(-5, 9));
            triangulationCheckList.Add(new Vector2D(-4, 3.6f));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-4, 18));
            triangulationCheckList.Add(new Vector2D(-2, 24));
            triangulationCheckList.Add(new Vector2D(-2, 18));
            triangulationCheckList.Add(new Vector2D(-4, 15));
            triangulationCheckList.Add(new Vector2D(-2, 15));
            triangulationCheckList.Add(new Vector2D(-2, 10));
            triangulationCheckList.Add(new Vector2D(-4, 15));
            triangulationCheckList.Add(new Vector2D(-2, 10));
            triangulationCheckList.Add(new Vector2D(-4, 10));
            triangulationCheckList.Add(new Vector2D(-4, 8.4f));
            triangulationCheckList.Add(new Vector2D(-2, 7.199999f));
            triangulationCheckList.Add(new Vector2D(-2, 4.799999f));
            triangulationCheckList.Add(new Vector2D(-4, 8.4f));
            triangulationCheckList.Add(new Vector2D(-2, 4.799999f));
            triangulationCheckList.Add(new Vector2D(-4, 3.6f));
            triangulationCheckList.Add(new Vector2D(-2, 24));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(0, 22));
            triangulationCheckList.Add(new Vector2D(-2, 24));
            triangulationCheckList.Add(new Vector2D(0, 22));
            triangulationCheckList.Add(new Vector2D(-2, 22));
            triangulationCheckList.Add(new Vector2D(-2, 18));
            triangulationCheckList.Add(new Vector2D(0, 18));
            triangulationCheckList.Add(new Vector2D(0, 15));
            triangulationCheckList.Add(new Vector2D(-2, 18));
            triangulationCheckList.Add(new Vector2D(0, 15));
            triangulationCheckList.Add(new Vector2D(-2, 15));
            triangulationCheckList.Add(new Vector2D(-2, 10));
            triangulationCheckList.Add(new Vector2D(0, 10));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(-2, 10));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(-2, 7.199999f));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(2, 24));
            triangulationCheckList.Add(new Vector2D(2, 22));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(2, 22));
            triangulationCheckList.Add(new Vector2D(0, 22));
            triangulationCheckList.Add(new Vector2D(0, 18));
            triangulationCheckList.Add(new Vector2D(2, 18));
            triangulationCheckList.Add(new Vector2D(2, 15));
            triangulationCheckList.Add(new Vector2D(0, 18));
            triangulationCheckList.Add(new Vector2D(2, 15));
            triangulationCheckList.Add(new Vector2D(0, 15));
            triangulationCheckList.Add(new Vector2D(0, 10));
            triangulationCheckList.Add(new Vector2D(2, 10));
            triangulationCheckList.Add(new Vector2D(2, 7.199999f));
            triangulationCheckList.Add(new Vector2D(0, 10));
            triangulationCheckList.Add(new Vector2D(2, 7.199999f));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(2, 24));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(2, 15));
            triangulationCheckList.Add(new Vector2D(2, 7.199999f));
            triangulationCheckList.Add(new Vector2D(5, 8.999998f));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(2, 7.199999f));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(2, 4.799999f));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(5, 8.999998f));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(5, 8.999998f));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(10, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(10, 15));
            triangulationCheckList.Add(new Vector2D(15, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TriangulateDoubleStarNonZero()
        {
            List<Vector2D> points = new List<Vector2D>();
            Polygon polygon = new Polygon();
            polygon.FillRule = FillRule.NonZero;
            points.Add(new Vector2D(-10, 0));
            points.Add(new Vector2D(0, 30));
            points.Add(new Vector2D(10, 0));
            points.Add(new Vector2D(-15, 15));
            points.Add(new Vector2D(15, 15));
                
            points.Add(new Vector2D(-10, 0));
            points.Add(new Vector2D(0, -30));
            points.Add(new Vector2D(10, 0));
            points.Add(new Vector2D(-15, -15));
            points.Add(new Vector2D(15, -15));

            polygon.Polygons.Add(new PolygonItem(points));

            var triangles = polygon.Fill();

            Assert.AreEqual(96, triangles.Count);

            List<Vector2D> triangulationCheckList = new List<Vector2D>();

            triangulationCheckList.Add(new Vector2D(-15, 15));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-10, 12));
            triangulationCheckList.Add(new Vector2D(-15, -15));
            triangulationCheckList.Add(new Vector2D(-10, -12));
            triangulationCheckList.Add(new Vector2D(-10, -15));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-10, 12));
            triangulationCheckList.Add(new Vector2D(-10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(-10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(-10, -12));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(-10, -12));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(-10, -15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(-5, -3));
            triangulationCheckList.Add(new Vector2D(-5, -15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(-5, -15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-5, -3));
            triangulationCheckList.Add(new Vector2D(0, -6));
            triangulationCheckList.Add(new Vector2D(0, -30));
            triangulationCheckList.Add(new Vector2D(-5, -3));
            triangulationCheckList.Add(new Vector2D(0, -30));
            triangulationCheckList.Add(new Vector2D(-5, -15));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(0, -6));
            triangulationCheckList.Add(new Vector2D(5, -3));
            triangulationCheckList.Add(new Vector2D(5, -15));
            triangulationCheckList.Add(new Vector2D(0, -6));
            triangulationCheckList.Add(new Vector2D(5, -15));
            triangulationCheckList.Add(new Vector2D(0, -30));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(5, -3));
            triangulationCheckList.Add(new Vector2D(6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(5, -3));
            triangulationCheckList.Add(new Vector2D(6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(5, -15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(10, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(10));
            triangulationCheckList.Add(new Vector2D(6.666667f, -9.999996f));
            triangulationCheckList.Add(new Vector2D(6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(10, -12));
            triangulationCheckList.Add(new Vector2D(10, -15));
            triangulationCheckList.Add(new Vector2D(6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(10, -15));
            triangulationCheckList.Add(new Vector2D(6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(10, 15));
            triangulationCheckList.Add(new Vector2D(15, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(10, -12));
            triangulationCheckList.Add(new Vector2D(15, -15));
            triangulationCheckList.Add(new Vector2D(10, -15));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TriangulateDoubleStarEvenOdd()
        {
            List<Vector2D> points = new List<Vector2D>();
            Polygon polygon = new Polygon();
            points.Add(new Vector2D(-10));
            points.Add(new Vector2D(0, 30));
            points.Add(new Vector2D(10));
            points.Add(new Vector2D(-15, 15));
            points.Add(new Vector2D(15, 15));
            points.Add(new Vector2D(-10));
            points.Add(new Vector2D(0, -30));
            points.Add(new Vector2D(10));
            points.Add(new Vector2D(-15, -15));
            points.Add(new Vector2D(15, -15));

            polygon.Polygons.Add(new PolygonItem(points));

            var triangles = polygon.Fill();

            Assert.AreEqual(108, triangles.Count);

            List<Vector2D> triangulationCheckList = new List<Vector2D>();

            triangulationCheckList.Add(new Vector2D(-15, 15));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-10, 12));
            triangulationCheckList.Add(new Vector2D(-15, -15));
            triangulationCheckList.Add(new Vector2D(-10, -12));
            triangulationCheckList.Add(new Vector2D(-10, -15));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-10, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-10, 12));
            triangulationCheckList.Add(new Vector2D(-10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(-10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(-10, -12));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(-10, -12));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(-10, -15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-5, 9));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(-5, -3));
            triangulationCheckList.Add(new Vector2D(-5, -9));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(-5, -9));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(-5, -15));
            triangulationCheckList.Add(new Vector2D(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(-5, 15));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(0, 15));
            triangulationCheckList.Add(new Vector2D(-5, 9));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(-5, 3));
            triangulationCheckList.Add(new Vector2D(-5, -3));
            triangulationCheckList.Add(new Vector2D(0, -6));
            triangulationCheckList.Add(new Vector2D(-5, -9));
            triangulationCheckList.Add(new Vector2D(-5, -15));
            triangulationCheckList.Add(new Vector2D(0, -15));
            triangulationCheckList.Add(new Vector2D(0, -30));
            triangulationCheckList.Add(new Vector2D(0, 30));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(0, 15));
            triangulationCheckList.Add(new Vector2D(0, 6));
            triangulationCheckList.Add(new Vector2D(5, 8.999998f));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(0, -6));
            triangulationCheckList.Add(new Vector2D(5, -3));
            triangulationCheckList.Add(new Vector2D(5, -9.000004f));
            triangulationCheckList.Add(new Vector2D(0, -15));
            triangulationCheckList.Add(new Vector2D(5, -15));
            triangulationCheckList.Add(new Vector2D(0, -30));
            triangulationCheckList.Add(new Vector2D(5, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(5, 8.999998f));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(5, 8.999998f));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(5, 3));
            triangulationCheckList.Add(new Vector2D(5, -3));
            triangulationCheckList.Add(new Vector2D(6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(5, -3));
            triangulationCheckList.Add(new Vector2D(6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(5, -9.000004f));
            triangulationCheckList.Add(new Vector2D(5, -15));
            triangulationCheckList.Add(new Vector2D(6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(10, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(6.666667f, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 10));
            triangulationCheckList.Add(new Vector2D(10));
            triangulationCheckList.Add(new Vector2D(6.666667f, 2));
            triangulationCheckList.Add(new Vector2D(6.666667f, -2));
            triangulationCheckList.Add(new Vector2D(10));
            triangulationCheckList.Add(new Vector2D(6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(10, -12));
            triangulationCheckList.Add(new Vector2D(10, -15));
            triangulationCheckList.Add(new Vector2D(6.666667f, -10));
            triangulationCheckList.Add(new Vector2D(10, -15));
            triangulationCheckList.Add(new Vector2D(6.666667f, -15));
            triangulationCheckList.Add(new Vector2D(10, 15));
            triangulationCheckList.Add(new Vector2D(15, 15));
            triangulationCheckList.Add(new Vector2D(10, 12));
            triangulationCheckList.Add(new Vector2D(10, -12));
            triangulationCheckList.Add(new Vector2D(15, -15));
            triangulationCheckList.Add(new Vector2D(10, -15));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }
    }
}
