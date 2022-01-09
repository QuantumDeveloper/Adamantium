using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class TorusTemplate : PrimitiveTemplate
    {
        private double diameter,
            thickness;

        public TorusTemplate(
            GeometryType geometryType,
            double diameter,
            double thickness,
            int tessellation = 3,
            Matrix4x4? transform = null) : base(geometryType, tessellation, transform)
        {
            this.diameter = diameter;
            this.thickness = thickness;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Torus;
            metadata.Diameter = diameter;
            metadata.Thickness = thickness;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Torus.GenerateGeometry(GeometryType, diameter, thickness, Tessellation, Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
