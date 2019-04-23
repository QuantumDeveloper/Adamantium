using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class TorusTemplate : PrimitiveTemplate
    {
        private float diameter,
            thickness;

        public TorusTemplate(
            GeometryType geometryType,
            float diameter,
            float thickness,
            int tessellation = 3,
            Matrix4x4F? transform = null,
            bool toRightHanded = false) : base(geometryType, tessellation, transform, toRightHanded)
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
            var primitive3D = Shapes.Torus.GenerateGeometry(GeometryType, diameter, thickness, Tessellation, Transform, ToRightHanded);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
