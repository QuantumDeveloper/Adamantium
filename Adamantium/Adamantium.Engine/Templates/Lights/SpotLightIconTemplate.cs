using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Lights
{
    public class SpotLightIconTemplate: LightIconTemplate
    {
        public override Entity BuildEntity(Entity owner, string name)
        {
            float size = 0.2f;
            var points = new List<Vector2>();
            points.Add(new Vector2(-size, size));
            points.Add(new Vector2(-size, 0));
            points.Add(new Vector2(size, 0));
            points.Add(new Vector2(size, 0));

            float range = 180;
            int tessellation = 20;
            float angle = range / tessellation;

            float angleItem = MathHelper.DegreesToRadians(angle);
            var startAngle = MathHelper.DegreesToRadians(0);
            angle = startAngle;
            var center = new Vector3F(0, size, 0);
            float radiusX = size;
            float radiusY = size;

            for (int i = 0; i <= tessellation; ++i)
            {
                float x = center.X + (radiusX * (float)Math.Cos(angle));
                float y = center.Y + (radiusY * (float)Math.Sin(angle));
                var vertex = new Vector2(x, y);

                points.Add(vertex);

                angle += angleItem;
            }

            PolygonItem item = new PolygonItem(points);
            Polygon polygon = new Polygon();
            polygon.AddItem(item);
            var triangulatedList = polygon.Fill();
            var spotLightIcon = new Mesh();
            spotLightIcon.MeshTopology = PrimitiveType.TriangleList;
            spotLightIcon.SetPositions(triangulatedList);
            spotLightIcon.Optimize();

            float height = 0.1f;
            float raySize = 0.3f;
            var centralRay = Shapes.Rectangle.GenerateGeometry(
                GeometryType.Solid, 
                raySize, 
                height, 
                new CornerRadius(height/2),
                tessellation, 
                Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(90))*Matrix4x4F.Translation(new Vector3F(0, (-raySize / 2)-0.2f, 0)));
            var leftRay = centralRay.Clone(Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(-45)) * Matrix4x4F.Translation(-size/2, 0, 0));
            var rightRay = centralRay.Clone(Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(45)) * Matrix4x4F.Translation(size/2, 0, 0));

            MergeInstance[] instances = new MergeInstance[3];
            var instance1 = new MergeInstance(leftRay, Matrix4x4F.Identity, false);
            instances[0] = instance1;
            var instance2 = new MergeInstance(centralRay, Matrix4x4F.Identity, false);
            instances[1] = instance2;
            var instance3 = new MergeInstance(rightRay, Matrix4x4F.Identity, false);
            instances[2] = instance3;

            spotLightIcon.Merge(instances);

            return BuildSubEntity(owner, name, Colors.White, spotLightIcon, BoundingVolume.OrientedBox);
        }
    }
}
