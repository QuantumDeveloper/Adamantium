using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class SphereTemplate : PrimitiveTemplate
    {
        private float diameter;
        private SphereType sphereType;

        public SphereTemplate(
            GeometryType geometryType,
            SphereType sphereType,
            float diameter,
            int tessellation = 3,
            Matrix4x4F? transform = null,
            bool toRightHanded = false) : base(geometryType, tessellation, transform, toRightHanded)
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
            var primitive3D = Shapes.Sphere.GenerateGeometry(GeometryType, sphereType, diameter, Tessellation, Transform, ToRightHanded);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D, BoundingVolume.Sphere));
        }
    }
}
