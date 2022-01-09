using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class PlaneTemplate : PrimitiveTemplate
    {
        private float width;
        private float length;

        public PlaneTemplate(
            GeometryType geometryType,
            float width,
            float length,
            int tessellation = 1,
            Matrix4x4? transform = null) : base(geometryType, tessellation, transform)
        {
            this.width = width;
            this.length = length;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Plane;
            metadata.Width = width;
            metadata.Depth = length;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Plane.GenerateGeometry(GeometryType, width, length, Tessellation);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}