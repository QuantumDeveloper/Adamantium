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
            var points = new List<Vector2>();
            Polygon polygon = new Polygon();
            polygon.FillRule = FillRule.NonZero;
            points.Add(new Vector2(-10));
            points.Add(new Vector2(0, 30));
            points.Add(new Vector2(10));
            points.Add(new Vector2(-15, 15));
            points.Add(new Vector2(15, 15));

            polygon.AddItem(new PolygonItem(points));

            var points2 = new List<Vector2>();

            points2.Add(new Vector2(-6, 18));
            points2.Add(new Vector2(2, 18));
            points2.Add(new Vector2(2, 10));
            points2.Add(new Vector2(-6, 10));

            polygon.AddItem(new PolygonItem(points2));

            points2.Clear();
            points2.Add(new Vector2(-2, 22));
            points2.Add(new Vector2(2, 22));
            points2.Add(new Vector2(2, 10));
            points2.Add(new Vector2(-2, 10));
            polygon.AddItem(new PolygonItem(points2));

            var triangles = polygon.Fill();

            Assert.AreEqual(84, triangles.Count);

            var triangulationCheckList = new List<Vector2>();
            triangulationCheckList.Add(new Vector2(-15,15));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-10, 12));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-6.666667, 15));
            triangulationCheckList.Add(new Vector2(-6.666667, 10));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-6.666667, 10));
            triangulationCheckList.Add(new Vector2(-10, 12));
            triangulationCheckList.Add(new Vector2(-10));


            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-6, 15));
            triangulationCheckList.Add(new Vector2(-6, 2.4));
            triangulationCheckList.Add(new Vector2(-6.666667, 15));
            triangulationCheckList.Add(new Vector2(-6, 2.4));
            triangulationCheckList.Add(new Vector2(-6.666667, 2));
            triangulationCheckList.Add(new Vector2(-6, 18));
            triangulationCheckList.Add(new Vector2(-5, 18));

            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-6, 18));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-6, 2.4));
            triangulationCheckList.Add(new Vector2(-5, 18));
            triangulationCheckList.Add(new Vector2(-4, 18));
            triangulationCheckList.Add(new Vector2(-4, 3.6));
            triangulationCheckList.Add(new Vector2(-5, 18));
            triangulationCheckList.Add(new Vector2(-4, 3.6));
            triangulationCheckList.Add(new Vector2(-5, 3));
            
            //30-39
            triangulationCheckList.Add(new Vector2(-4, 18));
            triangulationCheckList.Add(new Vector2(-2, 24));
            triangulationCheckList.Add(new Vector2(-2, 4.799999));
            triangulationCheckList.Add(new Vector2(-4, 18));
            triangulationCheckList.Add(new Vector2(-2, 4.799999));
            triangulationCheckList.Add(new Vector2(-4, 3.6));
            triangulationCheckList.Add(new Vector2(-2, 24));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(0, 22));
            triangulationCheckList.Add(new Vector2(-2, 24));

            //40-49
            triangulationCheckList.Add(new Vector2(0, 22));
            triangulationCheckList.Add(new Vector2(-2, 22));
            triangulationCheckList.Add(new Vector2(-2, 10));
            triangulationCheckList.Add(new Vector2(0, 10));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(-2, 10));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(-2, 4.799999f));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(2, 24));

            //50-59
            triangulationCheckList.Add(new Vector2(2, 22));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(2, 22));
            triangulationCheckList.Add(new Vector2(0, 22));
            triangulationCheckList.Add(new Vector2(0, 10));
            triangulationCheckList.Add(new Vector2(2, 10));
            triangulationCheckList.Add(new Vector2(2, 4.799999f));
            triangulationCheckList.Add(new Vector2(0, 10));
            triangulationCheckList.Add(new Vector2(2, 4.799999f));
            triangulationCheckList.Add(new Vector2(0, 6));

            //60-69
            triangulationCheckList.Add(new Vector2(2, 24));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(2, 24));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(2, 4.799999f));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(5, 15));

            //70-79
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(10, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(10));

            //80-84
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(10, 15));
            triangulationCheckList.Add(new Vector2(15, 15));
            triangulationCheckList.Add(new Vector2(10, 12));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TriangulateComplexPolygonEvenOdd()
        {
            List<Vector2> points = new List<Vector2>();
            Polygon polygon = new Polygon();
            points.Add(new Vector2(-10));
            points.Add(new Vector2(0, 30));
            points.Add(new Vector2(10));
            points.Add(new Vector2(-15, 15));
            points.Add(new Vector2(15, 15));

            polygon.AddItem(new PolygonItem(points));

            var points2 = new List<Vector2>();

            points2.Add(new Vector2(-6, 18));
            points2.Add(new Vector2(2, 18));
            points2.Add(new Vector2(2, 10));
            points2.Add(new Vector2(-6, 10));

            polygon.AddItem(new PolygonItem(points2));

            points2.Clear();
            points2.Add(new Vector2(-2, 22));
            points2.Add(new Vector2(2, 22));
            points2.Add(new Vector2(2, 10f));
            points2.Add(new Vector2(-2, 10f));
            polygon.AddItem(new PolygonItem(points2));

            var triangles = polygon.Fill();

            Assert.AreEqual(138, triangles.Count);

            List<Vector2> triangulationCheckList = new List<Vector2>();

            triangulationCheckList.Add(new Vector2(-15, 15));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-10, 12));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-10, 12));
            triangulationCheckList.Add(new Vector2(-10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-6, 15));
            triangulationCheckList.Add(new Vector2(-6, 12));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-6, 12));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-6, 9.6f));
            triangulationCheckList.Add(new Vector2(-6, 2.4f));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-6, 2.4f));
            triangulationCheckList.Add(new Vector2(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2(-6, 18));
            triangulationCheckList.Add(new Vector2(-5, 18));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(-6, 18));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(-6, 15));
            triangulationCheckList.Add(new Vector2(-6, 12));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(-5, 10));
            triangulationCheckList.Add(new Vector2(-6, 12));
            triangulationCheckList.Add(new Vector2(-5, 10));
            triangulationCheckList.Add(new Vector2(-6, 10));
            triangulationCheckList.Add(new Vector2(-6, 9.6f));
            triangulationCheckList.Add(new Vector2(-5, 9));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-6, 9.6f));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-6, 2.4f));
            triangulationCheckList.Add(new Vector2(-5, 18));
            triangulationCheckList.Add(new Vector2(-4, 18));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(-4, 15));
            triangulationCheckList.Add(new Vector2(-4, 10));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(-4, 10));
            triangulationCheckList.Add(new Vector2(-5, 10));
            triangulationCheckList.Add(new Vector2(-5, 9));
            triangulationCheckList.Add(new Vector2(-4, 8.4f));
            triangulationCheckList.Add(new Vector2(-4, 3.6f));
            triangulationCheckList.Add(new Vector2(-5, 9));
            triangulationCheckList.Add(new Vector2(-4, 3.6f));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-4, 18));
            triangulationCheckList.Add(new Vector2(-2, 24));
            triangulationCheckList.Add(new Vector2(-2, 18));
            triangulationCheckList.Add(new Vector2(-4, 15));
            triangulationCheckList.Add(new Vector2(-2, 15));
            triangulationCheckList.Add(new Vector2(-2, 10));
            triangulationCheckList.Add(new Vector2(-4, 15));
            triangulationCheckList.Add(new Vector2(-2, 10));
            triangulationCheckList.Add(new Vector2(-4, 10));
            triangulationCheckList.Add(new Vector2(-4, 8.4f));
            triangulationCheckList.Add(new Vector2(-2, 7.199999f));
            triangulationCheckList.Add(new Vector2(-2, 4.799999f));
            triangulationCheckList.Add(new Vector2(-4, 8.4f));
            triangulationCheckList.Add(new Vector2(-2, 4.799999f));
            triangulationCheckList.Add(new Vector2(-4, 3.6f));
            triangulationCheckList.Add(new Vector2(-2, 24));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(0, 22));
            triangulationCheckList.Add(new Vector2(-2, 24));
            triangulationCheckList.Add(new Vector2(0, 22));
            triangulationCheckList.Add(new Vector2(-2, 22));
            triangulationCheckList.Add(new Vector2(-2, 18));
            triangulationCheckList.Add(new Vector2(0, 18));
            triangulationCheckList.Add(new Vector2(0, 15));
            triangulationCheckList.Add(new Vector2(-2, 18));
            triangulationCheckList.Add(new Vector2(0, 15));
            triangulationCheckList.Add(new Vector2(-2, 15));
            triangulationCheckList.Add(new Vector2(-2, 10));
            triangulationCheckList.Add(new Vector2(0, 10));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(-2, 10));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(-2, 7.199999f));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(2, 24));
            triangulationCheckList.Add(new Vector2(2, 22));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(2, 22));
            triangulationCheckList.Add(new Vector2(0, 22));
            triangulationCheckList.Add(new Vector2(0, 18));
            triangulationCheckList.Add(new Vector2(2, 18));
            triangulationCheckList.Add(new Vector2(2, 15));
            triangulationCheckList.Add(new Vector2(0, 18));
            triangulationCheckList.Add(new Vector2(2, 15));
            triangulationCheckList.Add(new Vector2(0, 15));
            triangulationCheckList.Add(new Vector2(0, 10));
            triangulationCheckList.Add(new Vector2(2, 10));
            triangulationCheckList.Add(new Vector2(2, 7.199999f));
            triangulationCheckList.Add(new Vector2(0, 10));
            triangulationCheckList.Add(new Vector2(2, 7.199999f));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(2, 24));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(2, 15));
            triangulationCheckList.Add(new Vector2(2, 7.199999f));
            triangulationCheckList.Add(new Vector2(5, 8.999998f));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(2, 7.199999f));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(2, 4.799999f));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(5, 8.999998f));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(5, 8.999998f));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(10, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(10));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(10, 15));
            triangulationCheckList.Add(new Vector2(15, 15));
            triangulationCheckList.Add(new Vector2(10, 12));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TriangulateDoubleStarNonZero()
        {
            List<Vector2> points = new List<Vector2>();
            Polygon polygon = new Polygon();
            polygon.FillRule = FillRule.NonZero;
            points.Add(new Vector2(-10, 0));
            points.Add(new Vector2(0, 30));
            points.Add(new Vector2(10, 0));
            points.Add(new Vector2(-15, 15));
            points.Add(new Vector2(15, 15));
                
            points.Add(new Vector2(-10, 0));
            points.Add(new Vector2(0, -30));
            points.Add(new Vector2(10, 0));
            points.Add(new Vector2(-15, -15));
            points.Add(new Vector2(15, -15));

            polygon.AddItem(new PolygonItem(points));

            var triangles = polygon.Fill();

            Assert.AreEqual(96, triangles.Count);

            List<Vector2> triangulationCheckList = new List<Vector2>();

            triangulationCheckList.Add(new Vector2(-15, 15));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-10, 12));
            triangulationCheckList.Add(new Vector2(-15, -15));
            triangulationCheckList.Add(new Vector2(-10, -12));
            triangulationCheckList.Add(new Vector2(-10, -15));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-10, 12));
            triangulationCheckList.Add(new Vector2(-10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2(-10));
            triangulationCheckList.Add(new Vector2(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2(-10, -12));
            triangulationCheckList.Add(new Vector2(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2(-10, -12));
            triangulationCheckList.Add(new Vector2(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2(-10, -15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2(-5, -3));
            triangulationCheckList.Add(new Vector2(-5, -15));
            triangulationCheckList.Add(new Vector2(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2(-5, -15));
            triangulationCheckList.Add(new Vector2(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-5, -3));
            triangulationCheckList.Add(new Vector2(0, -6));
            triangulationCheckList.Add(new Vector2(0, -30));
            triangulationCheckList.Add(new Vector2(-5, -3));
            triangulationCheckList.Add(new Vector2(0, -30));
            triangulationCheckList.Add(new Vector2(-5, -15));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(0, -6));
            triangulationCheckList.Add(new Vector2(5, -3));
            triangulationCheckList.Add(new Vector2(5, -15));
            triangulationCheckList.Add(new Vector2(0, -6));
            triangulationCheckList.Add(new Vector2(5, -15));
            triangulationCheckList.Add(new Vector2(0, -30));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(5, -3));
            triangulationCheckList.Add(new Vector2(6.666667f, -2));
            triangulationCheckList.Add(new Vector2(6.666667f, -15));
            triangulationCheckList.Add(new Vector2(5, -3));
            triangulationCheckList.Add(new Vector2(6.666667f, -15));
            triangulationCheckList.Add(new Vector2(5, -15));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(10, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(10));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(6.666667f, -2));
            triangulationCheckList.Add(new Vector2(10));
            triangulationCheckList.Add(new Vector2(6.666667f, -9.999996f));
            triangulationCheckList.Add(new Vector2(6.666667f, -10));
            triangulationCheckList.Add(new Vector2(10, -12));
            triangulationCheckList.Add(new Vector2(10, -15));
            triangulationCheckList.Add(new Vector2(6.666667f, -10));
            triangulationCheckList.Add(new Vector2(10, -15));
            triangulationCheckList.Add(new Vector2(6.666667f, -15));
            triangulationCheckList.Add(new Vector2(10, 15));
            triangulationCheckList.Add(new Vector2(15, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(10, -12));
            triangulationCheckList.Add(new Vector2(15, -15));
            triangulationCheckList.Add(new Vector2(10, -15));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }

        [Test]
        public void TriangulateDoubleStarEvenOdd()
        {
            List<Vector2> points = new List<Vector2>();
            Polygon polygon = new Polygon();
            points.Add(new Vector2(-10));
            points.Add(new Vector2(0, 30));
            points.Add(new Vector2(10));
            points.Add(new Vector2(-15, 15));
            points.Add(new Vector2(15, 15));
            points.Add(new Vector2(-10));
            points.Add(new Vector2(0, -30));
            points.Add(new Vector2(10));
            points.Add(new Vector2(-15, -15));
            points.Add(new Vector2(15, -15));

            polygon.AddItem(new PolygonItem(points));

            var triangles = polygon.Fill();

            Assert.AreEqual(108, triangles.Count);

            List<Vector2> triangulationCheckList = new List<Vector2>();

            triangulationCheckList.Add(new Vector2(-15, 15));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-10, 12));
            triangulationCheckList.Add(new Vector2(-15, -15));
            triangulationCheckList.Add(new Vector2(-10, -12));
            triangulationCheckList.Add(new Vector2(-10, -15));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-10, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-10, 12));
            triangulationCheckList.Add(new Vector2(-10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2(-10));
            triangulationCheckList.Add(new Vector2(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2(-10, -12));
            triangulationCheckList.Add(new Vector2(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2(-10, -12));
            triangulationCheckList.Add(new Vector2(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2(-10, -15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 15));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-5, 9));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-6.666667f, 10));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-6.666667f, 2));
            triangulationCheckList.Add(new Vector2(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2(-5, -3));
            triangulationCheckList.Add(new Vector2(-5, -9));
            triangulationCheckList.Add(new Vector2(-6.666667f, -2));
            triangulationCheckList.Add(new Vector2(-5, -9));
            triangulationCheckList.Add(new Vector2(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2(-6.666667f, -10));
            triangulationCheckList.Add(new Vector2(-5, -15));
            triangulationCheckList.Add(new Vector2(-6.666667f, -15));
            triangulationCheckList.Add(new Vector2(-5, 15));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(0, 15));
            triangulationCheckList.Add(new Vector2(-5, 9));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(-5, 3));
            triangulationCheckList.Add(new Vector2(-5, -3));
            triangulationCheckList.Add(new Vector2(0, -6));
            triangulationCheckList.Add(new Vector2(-5, -9));
            triangulationCheckList.Add(new Vector2(-5, -15));
            triangulationCheckList.Add(new Vector2(0, -15));
            triangulationCheckList.Add(new Vector2(0, -30));
            triangulationCheckList.Add(new Vector2(0, 30));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(0, 15));
            triangulationCheckList.Add(new Vector2(0, 6));
            triangulationCheckList.Add(new Vector2(5, 8.999998f));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(0, -6));
            triangulationCheckList.Add(new Vector2(5, -3));
            triangulationCheckList.Add(new Vector2(5, -9.000004f));
            triangulationCheckList.Add(new Vector2(0, -15));
            triangulationCheckList.Add(new Vector2(5, -15));
            triangulationCheckList.Add(new Vector2(0, -30));
            triangulationCheckList.Add(new Vector2(5, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(5, 8.999998f));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(5, 8.999998f));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(5, 3));
            triangulationCheckList.Add(new Vector2(5, -3));
            triangulationCheckList.Add(new Vector2(6.666667f, -2));
            triangulationCheckList.Add(new Vector2(6.666667f, -10));
            triangulationCheckList.Add(new Vector2(5, -3));
            triangulationCheckList.Add(new Vector2(6.666667f, -10));
            triangulationCheckList.Add(new Vector2(5, -9.000004f));
            triangulationCheckList.Add(new Vector2(5, -15));
            triangulationCheckList.Add(new Vector2(6.666667f, -10));
            triangulationCheckList.Add(new Vector2(6.666667f, -15));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(10, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(6.666667f, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(6.666667f, 10));
            triangulationCheckList.Add(new Vector2(10));
            triangulationCheckList.Add(new Vector2(6.666667f, 2));
            triangulationCheckList.Add(new Vector2(6.666667f, -2));
            triangulationCheckList.Add(new Vector2(10));
            triangulationCheckList.Add(new Vector2(6.666667f, -10));
            triangulationCheckList.Add(new Vector2(6.666667f, -10));
            triangulationCheckList.Add(new Vector2(10, -12));
            triangulationCheckList.Add(new Vector2(10, -15));
            triangulationCheckList.Add(new Vector2(6.666667f, -10));
            triangulationCheckList.Add(new Vector2(10, -15));
            triangulationCheckList.Add(new Vector2(6.666667f, -15));
            triangulationCheckList.Add(new Vector2(10, 15));
            triangulationCheckList.Add(new Vector2(15, 15));
            triangulationCheckList.Add(new Vector2(10, 12));
            triangulationCheckList.Add(new Vector2(10, -12));
            triangulationCheckList.Add(new Vector2(15, -15));
            triangulationCheckList.Add(new Vector2(10, -15));

            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.AreEqual(triangulationCheckList[i], triangles[i]);
            }
        }
    }
}
