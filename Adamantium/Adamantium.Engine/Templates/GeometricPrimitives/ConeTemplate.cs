﻿using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public class ConeTemplate : PrimitiveTemplate
    {
        private float topDiameter,
            bottomDiameter,
            height;

        public ConeTemplate(
            GeometryType geometryType,
            float topDiameter,
            float bottomDiameter,
            float height,
            int tessellation = 3,
            Matrix4x4? transform = null) : base(geometryType, tessellation, transform)
        {
            this.topDiameter = topDiameter;
            this.bottomDiameter = bottomDiameter;
            this.height = height;
        }

        protected override void FillMetadata(
            MeshMetadata metadata)
        {
            metadata.GeometryType = GeometryType;
            metadata.ShapeType = ShapeType.Cone;
            metadata.TopDiameter = topDiameter;
            metadata.BottomDiameter = bottomDiameter;
            metadata.Height = height;
            metadata.TessellationFactor = Tessellation;
        }

        public override Task<Entity> BuildEntity(Entity owner)
        {
            var geometry = Shapes.Cone.GenerateGeometry(
                GeometryType, 
                height, 
                topDiameter, 
                bottomDiameter, 
                Tessellation,
                Transform);
            return Task.FromResult(BuildEntityFromPrimitive(owner, geometry));
        }
    }
}
