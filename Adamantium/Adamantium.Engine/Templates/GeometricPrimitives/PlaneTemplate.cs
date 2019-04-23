using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class PlaneTemplate : PrimitiveTemplate
    {
        private float _width;
        private float _length;

        public PlaneTemplate(
            GeometryType geometryType,
            float width,
            float length,
            int tessellation = 1,
            Matrix4x4F? transform = null,
            bool toRightHanded = false) : base(geometryType, tessellation, transform, toRightHanded)
        {
            this._width = width;
            this._length = length;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Plane;
            metadata.Width = _width;
            metadata.Depth = _length;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Plane.GenerateGeometry(GeometryType, _width, _length, Tessellation);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}