using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class RectangleTemplate : PrimitiveTemplate
    {
        private float width,
            height,
            radiusX,
            radiusY;

        public RectangleTemplate(
            GeometryType geometryType,
            float width = 2,
            float height = 1,
            float radiusX = 0,
            float radiusY = 0,
            int tessellation = 20,
            Matrix4x4F? transform = null) : base(geometryType, tessellation, transform)
        {
            this.width = width;
            this.height = height;
            this.radiusX = radiusX;
            this.radiusY = radiusY;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Rectangle;
            metadata.Width = width;
            metadata.Height = height;
            metadata.RadiusX = radiusX;
            metadata.RadiusY = radiusY;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var geometry = Shapes.Rectangle.GenerateGeometry(
                GeometryType, 
                width, 
                height, 
                radiusX, 
                radiusY,
                Tessellation, 
                Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, geometry));
        }
    }
}