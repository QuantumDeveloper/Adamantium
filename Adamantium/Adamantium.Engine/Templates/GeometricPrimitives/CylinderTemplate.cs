using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class CylinderTemplate : PrimitiveTemplate
    {
        private float _diameter,
            _height;

        public CylinderTemplate(
            GeometryType geometryType,
            float diameter,
            float height,
            int tessellation = 3,
            Matrix4x4F? transform = null,
            bool toRightHanded = false) : base(geometryType, tessellation, transform, toRightHanded)
        {
            this._diameter = diameter;
            this._height = height;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Cylinder;
            metadata.Diameter = _diameter;
            metadata.Height = _height;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Cylinder.GenerateGeometry(GeometryType, _height, _diameter, Tessellation, Transform, ToRightHanded);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
