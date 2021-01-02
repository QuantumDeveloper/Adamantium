using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class CubeTemplate : PrimitiveTemplate
    {
        private float width, height, depth;
        private GeometryType geometryType;

        public CubeTemplate(
            GeometryType geometryType,
            float width = 1,
            float height = 1,
            float depth = 1,
            int tessellation = 1,
            Matrix4x4F? transform = null) : base(geometryType, tessellation, transform)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;
            this.geometryType = geometryType;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Cube;
            metadata.Width = width;
            metadata.Height = height;
            metadata.Depth = depth;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Cube.GenerateGeometry(geometryType, width, height, depth, Tessellation);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
