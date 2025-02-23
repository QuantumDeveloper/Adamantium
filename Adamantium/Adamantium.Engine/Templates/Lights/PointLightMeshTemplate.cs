using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Graphics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;

namespace Adamantium.Engine.Templates.Lights;

public class PointLightMeshTemplate
{
    public Entity BuildEntity()
    {
        var pointLightMesh = Shapes.Sphere.GenerateGeometry(GeometryType.Solid, SphereType.UVSphere, 1, 40);

        var root = new Entity(null, "Point light mesh");
        var meshComponent = new MeshData();
        meshComponent.Mesh = pointLightMesh;

        var renderer = new MeshRenderer();
        root.Components.Add(meshComponent);
        root.Components.Add(renderer);

        return root;
    }
}