using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class LineTemplate : PrimitiveTemplate
    {
        private float _thickness;
        private Vector3F _startPoint;
        private Vector3F _endPoint;

        public LineTemplate(
            GeometryType geometryType,
            Vector3F startPoint,
            Vector3F endPoint,
            float thickness,
            Matrix4x4F? transform = null,
            bool toRightHanded = false) : base(geometryType, 1, transform, toRightHanded)
        {
            _startPoint = startPoint;
            _endPoint = endPoint;
           _thickness = thickness;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Ellipse;
            metadata.Thickness = _thickness;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Line.GenerateGeometry(GeometryType.Solid, _startPoint, _endPoint, _thickness);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
