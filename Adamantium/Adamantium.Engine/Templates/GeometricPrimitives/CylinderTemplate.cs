using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class CylinderTemplate : PrimitiveTemplate
    {
        private float diameter;
        private float height;

        public CylinderTemplate(
            GeometryType geometryType,
            float diameter,
            float height,
            int tessellation = 3,
            Matrix4x4? transform = null) : base(geometryType, tessellation, transform)
        {
            this.diameter = diameter;
            this.height = height;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Cylinder;
            metadata.Diameter = diameter;
            metadata.Height = height;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Cylinder.GenerateGeometry(GeometryType, height, diameter, Tessellation, Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
