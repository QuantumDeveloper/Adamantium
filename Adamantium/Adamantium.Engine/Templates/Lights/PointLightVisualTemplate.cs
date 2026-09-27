using Adamantium.ECS;
using Adamantium.Graphics;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;

namespace Adamantium.Engine.Templates.Lights;

public class PointLightVisualTemplate : LightVisualTemplate
{
    public override Entity BuildEntity(Entity owner, string name)
    {
        var sphere = Shapes.Sphere.GenerateGeometry(GeometryType.Outlined, SphereType.GeoSphere);
        var anchor = Shapes.Cube.GenerateGeometry(GeometryType.Solid, 1, 1);

        var root = BuildSubEntity(owner, name, Colors.Yellow, sphere);
        BuildSubEntity(root, "AnchorPointRight", Colors.Red, anchor, BoundingVolume.OrientedBox);
        BuildSubEntity(root, "AnchorPointLeft", Colors.Red, anchor, BoundingVolume.OrientedBox);
        BuildSubEntity(root, "AnchorPointUp", Colors.Red, anchor, BoundingVolume.OrientedBox);
        BuildSubEntity(root, "AnchorPointDown", Colors.Red, anchor, BoundingVolume.OrientedBox);
        BuildSubEntity(root, "AnchorPointForward", Colors.Red, anchor, BoundingVolume.OrientedBox);
        BuildSubEntity(root, "AnchorPointBackward", Colors.Red, anchor, BoundingVolume.OrientedBox);
        return root;
    }
}
