using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class RectangleTemplate : PrimitiveTemplate
    {
        private float width, height;
        private CornerRadius corners;

        public RectangleTemplate(
            GeometryType geometryType,
            float width = 2,
            float height = 1,
            CornerRadius corners = default,
            int tessellation = 20,
            Matrix4x4F? transform = null) : base(geometryType, tessellation, transform)
        {
            this.width = width;
            this.height = height;
            this.corners = corners;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Rectangle;
            metadata.Width = width;
            metadata.Height = height;
            metadata.Corners = corners;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var geometry = Shapes.Rectangle.GenerateGeometry(
                GeometryType, 
                width, 
                height, 
                corners, 
                Tessellation, 
                Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, geometry));
        }
    }
}