using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Engine.Templates.GeometricPrimitives;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;
using NUnit.Framework;

namespace Adamantium.EngineTests;

[TestFixture]
public class PrimitiveTemplateTests
{
    [Test]
    public void Ellipse_RunsFromItsStartAngleToItsStopAngle()
    {
        var template = new EllipseTemplate(GeometryType.Outlined, EllipseType.EdgeToEdge, new Vector2(2), 90, 180);

        var entity = template.BuildEntity(new Entity(null, "Arc")).GetAwaiter().GetResult();

        var meshData = entity.GetComponent<MeshData>();
        var points = meshData.Mesh.Contours[0].Points;
        Assert.That(points[0].X, Is.EqualTo(0).Within(1e-3));
        Assert.That(points[0].Y, Is.EqualTo(1).Within(1e-3));
        Assert.That(points[^1].X, Is.EqualTo(-1).Within(1e-3));
        Assert.That(points[^1].Y, Is.EqualTo(0).Within(1e-3));
        Assert.That(meshData.Metadata.StartAngle, Is.EqualTo(90).Within(1e-3));
        Assert.That(meshData.Metadata.StopAngle, Is.EqualTo(180).Within(1e-3));
    }

    [Test]
    public void Arc_KeepsItsStartAngle()
    {
        var template = new ArcTemplate(GeometryType.Outlined, new Vector2(2), 0.2f, 90, 180);

        var entity = template.BuildEntity(new Entity(null, "Arc")).GetAwaiter().GetResult();

        var metadata = entity.GetComponent<MeshData>().Metadata;
        Assert.That(metadata.StartAngle, Is.EqualTo(90).Within(1e-3));
        Assert.That(metadata.StopAngle, Is.EqualTo(180).Within(1e-3));
    }
}
