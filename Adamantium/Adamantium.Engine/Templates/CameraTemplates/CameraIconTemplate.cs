using System;
using System.Collections.Generic;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.CameraTemplates
{
    public class CameraIconTemplate
    {
        public Entity BuildEntity(Entity owner, string name)
        {
            var rectangle = Shapes.Rectangle.GenerateGeometry(GeometryType.Outlined, 0.5f, 0.3f, new CornerRadius(0.05f), 10);

            var ellipse1 = Shapes.Ellipse.GenerateGeometry(
                GeometryType.Outlined, 
                EllipseType.EdgeToEdge,
                new Vector2(0.3f), 
                0, 
                360, 
                true, 
                40, 
                Matrix4x4.Translation(-0.25f, 0.15f, 0));
            var ellipse2 = ellipse1.Clone(Matrix4x4.Translation(0.24, 0.12, 0));

            var lensPoints = new List<Vector2>();
            lensPoints.Add(new Vector2(0.24, 0.05));
            lensPoints.Add(new Vector2(0.3, 0.1));
            lensPoints.Add(new Vector2(0.4, 0.1));
            lensPoints.Add(new Vector2(0.4, -0.1));
            lensPoints.Add(new Vector2(0.3, -0.1));
            lensPoints.Add(new Vector2(0.24, -0.05));

            var polygon = new Polygon();
            var cameraBase = rectangle.GetContour(0);
            var ell1 = ellipse1.GetContour(0);
            var ell2 = ellipse2.GetContour(0);
            var lens = new MeshContour(lensPoints);
            polygon.FillRule = FillRule.NonZero;
            polygon.AddItems(cameraBase, ell1,ell2,lens);
            var result = polygon.Fill();

            Mesh mesh = new Mesh();
            mesh.SetPoints(result);
            mesh.Optimize();

            var root = BuildSubEntity(owner, name, Colors.White, mesh, BoundingVolume.OrientedBox);
            return root;
        }

        protected Entity BuildSubEntity(Entity owner, String name, Color color, Mesh geometry, BoundingVolume volume = BoundingVolume.None, float transparency = 1.0f)
        {
            Material material = new Material();
            material.MeshColor = color.ToVector3();
            material.HighlightColor = Colors.Yellow.ToVector4();
            material.Transparency = transparency;

            var entity = new Entity(owner, name);

            var meshData = new MeshData();
            meshData.Mesh = geometry;
            entity.AddComponent(meshData);
            Collider collider = null;
            switch (volume)
            {
                case BoundingVolume.OrientedBox:
                    collider = new BoxCollider();
                    entity.AddComponent(collider);
                    break;
                case BoundingVolume.Sphere:
                    collider = new SphereCollider();
                    entity.AddComponent(collider);
                    break;
            }

            entity.AddComponent(material);

            MeshRenderer renderer = new MeshRenderer();
            entity.AddComponent(renderer);

            return entity;
        }
    }
}
