using System;
using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Lights
{
    public class PointLightIconTemplate : LightIconTemplate
    {
        public override Entity BuildEntity(Entity owner, string name)
        {
            var range = 360.0f;
            var tessellation = 60;
            float angle = range / tessellation;

            var bulbPoints = new List<Vector2D>();

            var angleItem = MathHelper.DegreesToRadians(angle);
            var startAngle = MathHelper.DegreesToRadians(0.0f);
            angle = startAngle;
            var center = new Vector3D(0, 0.0f, 0);
            var radiusX = 0.2;
            var radiusY = 0.21;
            var triggerAngle1 = MathHelper.DegreesToRadians(315);
            var triggerAngle2 = MathHelper.DegreesToRadians(225);
            Vector2D leftLineStart = Vector2D.Zero;
            Vector2D rightLineStart = Vector2D.Zero;

            for (int i = 0; i <= tessellation; ++i)
            {
                var x = center.X + (radiusX * Math.Cos(angle));
                var y = center.Y + (radiusY * Math.Sin(angle));

                var vertex = new Vector2D(x, y);

                bulbPoints.Add(vertex);

                angle += angleItem;
            }

            rightLineStart.X = center.X + (radiusX * Math.Cos(triggerAngle1));
            rightLineStart.Y = center.Y + (radiusY * Math.Sin(triggerAngle1));

            leftLineStart.X = center.X + (radiusX * Math.Cos(triggerAngle2));
            leftLineStart.Y = center.Y + (radiusY * Math.Sin(triggerAngle2));

            var basePartSize = 0.05;
            var smallSize = 0.025;
            var bottomPartStartCoords = 0.25;
            var bulbBasePoints1 = new List<Vector2D>();
            bulbBasePoints1.Add(leftLineStart);
            bulbBasePoints1.Add(new Vector2D(-basePartSize, -bottomPartStartCoords));
            bulbBasePoints1.Add(new Vector2D(basePartSize, -bottomPartStartCoords));
            bulbBasePoints1.Add(rightLineStart);

            bottomPartStartCoords = 0.29;
            var bulbBasePoints2 = new List<Vector2D>();
            bulbBasePoints2.Add(new Vector2D(basePartSize, -bottomPartStartCoords));
            bulbBasePoints2.Add(new Vector2D(basePartSize, -bottomPartStartCoords + smallSize));
            bulbBasePoints2.Add(new Vector2D(-basePartSize, -bottomPartStartCoords + smallSize));
            bulbBasePoints2.Add(new Vector2D(-basePartSize, -bottomPartStartCoords));

            bottomPartStartCoords = 0.33;
            var bulbBasePoints3 = new List<Vector2D>();
            bulbBasePoints3.Add(new Vector2D(basePartSize, -bottomPartStartCoords));
            bulbBasePoints3.Add(new Vector2D(basePartSize, -bottomPartStartCoords + smallSize));
            bulbBasePoints3.Add(new Vector2D(-basePartSize, -bottomPartStartCoords + smallSize));
            bulbBasePoints3.Add(new Vector2D(-basePartSize, -bottomPartStartCoords));

            var bulbBasePoints4 = new List<Vector2D>();
            var baseStartCoords = 0.35;
            range = 180;
            tessellation = 40;
            angle = range / tessellation;

            angleItem = MathHelper.DegreesToRadians(angle);
            startAngle = MathHelper.DegreesToRadians(-180);
            angle = startAngle;
            center = new Vector3D(0, -baseStartCoords, 0);
            radiusX = basePartSize;
            radiusY = basePartSize / 2;

            for (int i = 0; i <= tessellation; ++i)
            {
                var x = center.X + (radiusX * Math.Cos(angle));
                var y = center.Y + (radiusY * Math.Sin(angle));

                var vertex = new Vector2D(x, y);

                bulbBasePoints4.Add(vertex);

                angle += angleItem;
            }

            PolygonItem bulb = new PolygonItem(bulbPoints) { Name = "Lightbulb" };
            PolygonItem bulbBase = new PolygonItem(bulbBasePoints1) { Name = "Bulb_Base" };
            PolygonItem base3 = new PolygonItem(bulbBasePoints2) { Name = "Base_2" };
            PolygonItem base2 = new PolygonItem(bulbBasePoints3) { Name = "Base_3" };
            PolygonItem base1 = new PolygonItem(bulbBasePoints4) { Name = "Base_4" };

            Polygon polygon = new Polygon();
            polygon.AddItems(base1, base2, base3, bulbBase, bulb);

            polygon.FillRule = FillRule.NonZero;
            Stopwatch timer = Stopwatch.StartNew();
            var triangulatedList = polygon.Fill();
            timer.Stop();
            var pointLightIcon = new Mesh();
            pointLightIcon.MeshTopology = PrimitiveType.TriangleList;
            pointLightIcon.SetPositions(triangulatedList);
            pointLightIcon.GenerateBasicIndices();
            pointLightIcon.Optimize();

            var meshes = new List<Mesh>();

            tessellation = 20;

            float height = 0.1f;
            float raySize = 0.3f;
            startAngle = -45;
            range = 360;
            angleItem = range / 8;
            angle = startAngle;
            var ray = Shapes.Rectangle.GenerateGeometry(
                GeometryType.Solid, 
                raySize, 
                height, 
                new CornerRadius(height / 2),
                tessellation, 
                Matrix4x4F.Translation(new Vector3F((raySize / 2) + 0.3f, 0, 0)));
            for (int i = 0; i < 7; i++)
            {
                meshes.Add(ray.Clone(Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(angle))));
                angle += angleItem;
            }

            MergeInstance[] instances = new MergeInstance[meshes.Count];
            for (int i = 0; i < instances.Length; i++)
            {
                var inst = new MergeInstance(meshes[i], Matrix4x4F.Identity, false);
                instances[i] = inst;
            }

            pointLightIcon.Merge(instances);

            return BuildSubEntity(owner, name, Colors.White, pointLightIcon, BoundingVolume.OrientedBox);
        }
    }
}
