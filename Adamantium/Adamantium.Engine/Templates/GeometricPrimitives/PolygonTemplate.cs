using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class PolygonTemplate : PrimitiveTemplate
    {
        private Vector2F diameter;

        public PolygonTemplate(
            GeometryType geometryType,
            Vector2F diameter,
            int tessellation,
            Matrix4x4F? transform = null,
            bool toRightHanded = false) : base(geometryType, tessellation, transform, toRightHanded)
        {
            this.diameter = diameter;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Polygon;
            metadata.Width = diameter.X;
            metadata.Height = diameter.Y;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive = Shapes.Polygon.GenerateGeometry(GeometryType, diameter, Tessellation, Transform, ToRightHanded);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive));
        }
    }
}
