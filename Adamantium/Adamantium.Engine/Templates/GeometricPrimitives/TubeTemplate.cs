using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class TubeTemplate : PrimitiveTemplate
    {
        private double diameter, height, thickness;

        public TubeTemplate(
            GeometryType geometryType,
            double diameter,
            double height,
            double thickness,
            int tessellation = 3,
            Matrix4x4? transform = null) : base(geometryType, tessellation, transform)
        {
            this.diameter = diameter;
            this.height = height;
            this.thickness = thickness;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Tube;
            metadata.Diameter = diameter;
            metadata.Height = height;
            metadata.Thickness = thickness;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Tube.GenerateGeometry(GeometryType, diameter, height, thickness, Tessellation, Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
