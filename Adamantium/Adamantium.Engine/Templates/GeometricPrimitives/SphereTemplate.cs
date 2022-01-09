using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class SphereTemplate : PrimitiveTemplate
    {
        private double diameter;
        private SphereType sphereType;

        public SphereTemplate(
            GeometryType geometryType,
            SphereType sphereType,
            double diameter,
            int tessellation = 3,
            Matrix4x4? transform = null) : base(geometryType, tessellation, transform)
        {
            this.diameter = diameter;
            this.sphereType = sphereType;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = (ShapeType)sphereType;
            metadata.Diameter = diameter;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Sphere.GenerateGeometry(GeometryType, sphereType, diameter, Tessellation, Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D, BoundingVolume.Sphere));
        }
    }
}
