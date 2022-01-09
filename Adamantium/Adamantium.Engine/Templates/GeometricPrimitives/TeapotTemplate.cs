using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class TeapotTemplate : PrimitiveTemplate
    {
        private float size;

        public TeapotTemplate(
            float size,
            int tessellation = 1,
            Matrix4x4? transform = null) : base(GeometryType.Solid, tessellation, transform)
        {
            this.size = size;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Teapot;
            metadata.Width = size;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Teapot.GenerateGeometry(GeometryType, size, Tessellation, Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
