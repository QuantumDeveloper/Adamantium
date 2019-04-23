using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class EllipseTemplate : PrimitiveTemplate
    {
        private EllipseType ellipseType;
        private Vector2F diameter;
        private float startAngle,
            stopAngle;

        private bool isClockwise;

        public EllipseTemplate(
            GeometryType geometryType,
            EllipseType ellipseType,
            Vector2F diameter,
            float startAngle = 0,
            float stopAngle = 360,
            bool isClockwise = true,
            int tessellation = 40,
            Matrix4x4F? transform = null,
            bool toRightHanded = false) : base(geometryType, tessellation, transform, toRightHanded)
        {
            this.diameter = diameter;
            this.ellipseType = ellipseType;
            this.stopAngle = startAngle;
            this.stopAngle = stopAngle;
            this.isClockwise = isClockwise;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Ellipse;
            metadata.Width = diameter.X;
            metadata.Height = diameter.Y;
            metadata.StartAngle = startAngle;
            metadata.StopAngle = stopAngle;
            metadata.EllipseType = ellipseType;
            metadata.IsClockwise = isClockwise;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var geometry = Shapes.Ellipse.GenerateGeometry(GeometryType, ellipseType, diameter, startAngle, stopAngle, isClockwise, Tessellation, Transform, ToRightHanded);
            return Task.FromResult(BuildEntityFromPrimitive(owner, geometry));
        }
    }
}