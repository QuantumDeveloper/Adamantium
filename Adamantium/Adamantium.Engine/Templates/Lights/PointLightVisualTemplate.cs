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

            var rect0 = Shapes.Cube.GenerateGeometry(GeometryType.Solid, 0.02f, 1, Matrix4x4F.Translation(0.5f, 0, 0));

            var rect1 = rect0.Clone(Matrix4x4F.RotationY(MathHelper.DegreesToRadians(90)));
            var rect2 = rect1.Clone(Matrix4x4F.RotationY(MathHelper.DegreesToRadians(90)));
            var rect3 = rect2.Clone(Matrix4x4F.RotationY(MathHelper.DegreesToRadians(90)));
            var rect4 = rect0.Clone(Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(90)));
            var rect5 = rect4.Clone(Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(180)));

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
