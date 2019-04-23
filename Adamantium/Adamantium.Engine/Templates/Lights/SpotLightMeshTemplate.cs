using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Lights
{
    public class SpotLightMeshTemplate
    {
        public Entity BuildEntity()
        {
            var transform = Matrix4x4F.Translation(0, -0.5f, 0);
            var cone = Shapes.Cone.GenerateGeometry(GeometryType.Solid, 1, 0, 1, 40, transform);

            var root = new Entity(null, "Spot light mesh");
            var meshComponent = new MeshData();
            meshComponent.Mesh = cone;

            var renderer = new MeshRenderer();
            root.Components.Add(meshComponent);
            root.Components.Add(renderer);

            return root;
        }
    }
}
