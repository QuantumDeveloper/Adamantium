using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class LineTemplate : PrimitiveTemplate
    {
        private float thickness;
        private Vector3F startPoint;
        private Vector3F endPoint;

        public LineTemplate(
            GeometryType geometryType,
            Vector3F startPoint,
            Vector3F endPoint,
            float thickness,
            Matrix4x4F? transform = null) : base(geometryType, 1, transform)
        {
           this. startPoint = startPoint;
           this. endPoint = endPoint;
           this.thickness = thickness;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Ellipse;
            metadata.Thickness = thickness;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var primitive3D = Shapes.Line.GenerateGeometry(GeometryType.Solid, startPoint, endPoint, thickness);
            return Task.FromResult(BuildEntityFromPrimitive(owner, primitive3D));
        }
    }
}
