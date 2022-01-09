using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class CapsuleTemplate : PrimitiveTemplate
    {
        private float diameter;
        private float height;

        public CapsuleTemplate(
            GeometryType geometryType,
            float diameter,
            float height,
            int tessellation,
            Matrix4x4? transform = null) : base(geometryType, tessellation, transform)
        {
            this.height = height;
            this.diameter = diameter;
        }

        protected override void FillMetadata(MeshMetadata metadata)
        {
            metadata.ShapeType = ShapeType.Capsule;
            metadata.GeometryType = GeometryType;
            metadata.Diameter = diameter;
            metadata.Height = height;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(
            Entity owner)
        {
            var primitive3D = Shapes.Capsule.GenerateGeometry(GeometryType, height, diameter, Tessellation, Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
