using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class ArcTemplate : PrimitiveTemplate
    {
        private float thickness;
        private Vector2F diameter;
        private float startAngle = 0;
        private float stopAngle = 360;
        private bool isClockwise = true;

        public ArcTemplate(
            GeometryType geometryType,
            Vector2F diameter,
            float thickness,
            float startAngle = 0,
            float stopAngle = 360,
            bool isClockwise = true,
            int tessellation = 40,
            Matrix4x4F? transform = null,
            bool toRightHanded = false) : base(geometryType, tessellation, transform, toRightHanded)
        {
            this.diameter = diameter;
            this.thickness = thickness;
            this.stopAngle = startAngle;
            this.stopAngle = stopAngle;
            this.isClockwise = isClockwise;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Arc;
            metadata.Width = diameter.X;
            metadata.Height = diameter.Y;
            metadata.StartAngle = startAngle;
            metadata.StopAngle = stopAngle;
            metadata.Thickness = thickness;
            metadata.IsClockwise = isClockwise;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var geometry = Shapes.Arc.GenerateGeometry(GeometryType, diameter, thickness, startAngle, stopAngle, isClockwise, Tessellation, Transform, ToRightHanded);
            return Task.FromResult(BuildEntityFromPrimitive(owner, geometry));
        }
    }
}