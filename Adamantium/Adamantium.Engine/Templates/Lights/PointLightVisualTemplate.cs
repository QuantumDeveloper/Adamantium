using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Lights
{
    public class PointLightVisualTemplate : LightVisualTemplate
    {
        public override Entity BuildEntity(Entity owner, string name)
        {
            var sphere = Shapes.Sphere.GenerateGeometry(GeometryType.Outlined, SphereType.GeoSphere);
            BuildSubEntity(owner, name, Colors.Yellow, sphere, BoundingVolume.Sphere);

            var rect0 = Shapes.Cube.GenerateGeometry(GeometryType.Solid, 0.02, 1, Matrix4x4.Translation(0.5, 0, 0));

            var rotationY = Matrix4x4.RotationY(MathHelper.DegreesToRadians(90));
            var rotationZ1 = Matrix4x4.RotationZ(MathHelper.DegreesToRadians(90));
            var rotationZ2 = Matrix4x4.RotationZ(MathHelper.DegreesToRadians(180));
            var rect1 = rect0.Clone(rotationY);
            var rect2 = rect1.Clone(rotationY);
            var rect3 = rect2.Clone(rotationY);
            var rect4 = rect0.Clone(rotationZ1);
            var rect5 = rect4.Clone(rotationZ2);

            var root = BuildSubEntity(owner, name, Colors.Yellow, sphere);
            BuildSubEntity(root, "AnchorPointRight", Colors.Red, rect0, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointBackward", Colors.Red, rect3, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointLeft", Colors.Red, rect2, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointForward", Colors.Red, rect1, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointUp", Colors.Red, rect4, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointDown", Colors.Red, rect5, BoundingVolume.OrientedBox);
            return root;
        }
    }
}
