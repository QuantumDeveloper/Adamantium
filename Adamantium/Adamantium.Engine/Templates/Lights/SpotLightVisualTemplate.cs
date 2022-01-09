using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Lights
{
    public class SpotLightVisualTemplate: LightVisualTemplate
    {
        public override Entity BuildEntity(Entity owner, string name)
        {
            var transform = Matrix4x4.Translation(0, -0.5f, 0);
            var cone = Shapes.Cone.GenerateGeometry(GeometryType.Outlined, 1, 0, 1, 40, transform);

            var rect0 = Shapes.Cube.GenerateGeometry(GeometryType.Solid, 0.02f, 1, Matrix4x4.Translation(0, -1, 0));

            var rotationY = Matrix4x4.RotationY(MathHelper.DegreesToRadians(90));
            var rect1 = rect0.Clone(Matrix4x4.Translation(0.5f, 0, 0));
            var rect2 = rect1.Clone(rotationY);
            var rect3 = rect2.Clone(rotationY);
            var rect4 = rect3.Clone(rotationY);

            var root = BuildSubEntity(owner, name, Colors.Yellow, cone);
            BuildSubEntity(root, "AnchorPointCenter", Colors.Green, rect0, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointRight", Colors.Red, rect1, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointForward", Colors.Red, rect2, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointLeft", Colors.Red, rect3, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "AnchorPointBackward", Colors.Red, rect4, BoundingVolume.OrientedBox);
            return root;
        }
    }
}
