using System;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

[TestFixture]
public class TriangleTreeTests
{
    [Test]
    public void RandomSoup_HitsWhatTestingEveryTriangleHits()
    {
        var random = new Random(7);
        var points = new Vector3[3000 * 3];
        for (int i = 0; i < points.Length; i += 3)
        {
            var center = new Vector3(Next(random) * 50, Next(random) * 50, Next(random) * 50);
            for (int k = 0; k < 3; k++)
            {
                points[i + k] = center + new Vector3(Next(random) * 3, Next(random) * 3, Next(random) * 3);
            }
        }

        var tree = new TriangleTree(points, [], PrimitiveType.TriangleList);

        for (int i = 0; i < 1000; i++)
        {
            var from = new Vector3F(Next(random), Next(random), Next(random)) * 120;
            var to = new Vector3F(Next(random), Next(random), Next(random)) * 40;
            var ray = new Ray(from, Vector3F.Normalize(to - from));

            var expected = EveryTriangle(points, ray, out var expectedDistance);
            var found = tree.Intersect(ray, out var distance);

            Assert.That(found, Is.EqualTo(expected), $"ray {i}");
            if (expected)
            {
                Assert.That(distance, Is.EqualTo(expectedDistance), $"ray {i}");
            }
        }
    }

    [Test]
    public void Strip_SkipsWhatARestartSeparates()
    {
        Vector3[] points =
        [
            new(-1, -1, 10), new(-1, 1, 10), new(1, -1, 10), new(1, 1, 10),
            new(4, -1, 20), new(4, 1, 20), new(6, -1, 20), new(6, 1, 20)
        ];
        var tree = new TriangleTree(points, [0, 1, 2, 3, -1, 4, 5, 6, 7], PrimitiveType.TriangleStrip);

        Assert.That(tree.TriangleCount, Is.EqualTo(4));
        Assert.That(tree.Intersect(new Ray(new Vector3F(5, 0, 0), Vector3F.UnitZ), out var distance), Is.True);
        Assert.That(distance, Is.EqualTo(20f).Within(1e-4f));
        Assert.That(tree.Intersect(new Ray(new Vector3F(2.5f, 0, 0), Vector3F.UnitZ), out _), Is.False);
    }

    [Test]
    public void Mesh_KeepsItsTree_UntilItsPointsMove()
    {
        var mesh = new Mesh(PrimitiveType.TriangleList)
            .SetPoints([new Vector3(-1, -1, 10), new Vector3(1, -1, 10), new Vector3(0, 1, 10)])
            .SetIndices([0, 1, 2]);
        var ray = new Ray(Vector3F.Zero, Vector3F.UnitZ);

        var first = mesh.GetTriangleTree();
        Assert.That(mesh.GetTriangleTree(), Is.SameAs(first));

        mesh.ApplyTransform(Matrix4x4.Translation(0, 0, 5));

        Assert.That(mesh.GetTriangleTree(), Is.Not.SameAs(first));
        Assert.That(mesh.GetTriangleTree().Intersect(ray, out var distance), Is.True);
        Assert.That(distance, Is.EqualTo(15f).Within(1e-4f));
    }

    [Test]
    public void Lines_HaveNoTriangles()
    {
        var tree = new TriangleTree([new Vector3(0, 0, 0), new Vector3(1, 0, 0)], [], PrimitiveType.LineList);

        Assert.That(tree.TriangleCount, Is.Zero);
        Assert.That(tree.Intersect(new Ray(Vector3F.Zero, Vector3F.UnitX), out _), Is.False);
    }

    private static float Next(Random random)
    {
        return (float)(random.NextDouble() * 2 - 1);
    }

    private static bool EveryTriangle(Vector3[] points, Ray ray, out float nearest)
    {
        nearest = float.MaxValue;
        var found = false;
        for (int i = 0; i < points.Length; i += 3)
        {
            var a = (Vector3F)points[i];
            var b = (Vector3F)points[i + 1];
            var c = (Vector3F)points[i + 2];
            if (Collision.RayIntersectsTriangle(ref ray, ref a, ref b, ref c, out float distance) && distance < nearest)
            {
                nearest = distance;
                found = true;
            }
        }

        return found;
    }
}
