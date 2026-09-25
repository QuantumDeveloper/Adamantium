using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Engine.Templates.CameraTemplates;
using Adamantium.Engine.Templates.Lights;
using Adamantium.Graphics.Core.Extensions;
using NUnit.Framework;

namespace Adamantium.EngineTests;

[TestFixture]
public class IconMeshTests
{
    private static Entity[] Icons() =>
    [
        new PointLightIconTemplate().BuildEntity(null, "Point"),
        new SpotLightIconTemplate().BuildEntity(null, "Spot"),
        new DirectionalLightIconTemplate().BuildEntity(null, "Directional"),
        new CameraIconTemplate().BuildEntity(null, "Camera")
    ];

    [Test]
    public void EveryIcon_TurnsIntoVertices()
    {
        foreach (var icon in Icons())
        {
            var mesh = icon.GetComponent<MeshData>().Mesh;
            Assert.DoesNotThrow(() => mesh.ToMeshVertices(),
                $"{icon.Name}: {mesh.Points.Length} points, {mesh.Normals?.Length ?? 0} normals, semantic {mesh.Semantic}");
        }
    }
}
