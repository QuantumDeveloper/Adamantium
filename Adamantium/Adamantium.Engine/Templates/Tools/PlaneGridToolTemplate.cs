using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Tools
{
    public class PlaneGridToolTemplate : BaseToolTemplate
    {
        private float width;
        private float length;
        private int tessellation;

        public PlaneGridToolTemplate(float width, float length, Vector3F scale, int tessellation)
        {
            this.width = width;
            this.length = length;
            baseScale = scale;
            this.tessellation = tessellation;
        }

        public override Entity BuildEntity(Entity owner, string name)
        {
            var root = new Entity(owner, name);
            var planeGrid = Shapes.PlaneGrid.GenerateGeometry(width, length, tessellation);
            BuildSubEntity(root, "Grid", planeGrid, Colors.Black, BoundingVolume.OrientedBox);

            float startPointX = width / 2;
            float startPointY = 0;
            Vector3F startPoint = new Vector3F(-startPointX, -startPointY, 0);
            Vector3F endPoint = new Vector3F(startPointX, -startPointY, 0);

            Matrix4x4F transform = Matrix4x4F.RotationX(MathHelper.DegreesToRadians(90));
            var line = Shapes.Line.GenerateGeometry(GeometryType.Solid, startPoint, endPoint, 0.05f, transform);
            BuildSubEntity(root, "Line1", line, Colors.Black, BoundingVolume.OrientedBox);
            transform = Matrix4x4F.RotationY(MathHelper.DegreesToRadians(90));
            var line2 = line.Clone(transform);
            BuildSubEntity(root, "Line2", line2, Colors.Black, BoundingVolume.OrientedBox);

            return root;
        }
    }
}
