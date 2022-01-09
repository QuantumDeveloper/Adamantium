﻿using System.Collections.Generic;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Lights
{
    public class DirectionalLightIconTemplate:LightIconTemplate
    {
        public override Entity BuildEntity(Entity owner, string name)
        {
            int tessellation = 20;
            var diameter = new Vector2(0.4);

            float height = 0.1f;
            float raySize = 0.3f;
            var circle = Shapes.Ellipse.GenerateGeometry(GeometryType.Solid, EllipseType.EdgeToEdge, diameter);

            var meshes = new List<Mesh>();
            float startAngle = 0;
            float range = 360;
            float angleItem = range / 8;
            float angle = startAngle;
            var ray = Shapes.Rectangle.GenerateGeometry(GeometryType.Solid, 
                raySize, 
                height,
                new CornerRadius(height / 2), 
                tessellation,
                Matrix4x4.Translation(new Vector3((raySize / 2) + 0.3f, 0, 0)));
            for (int i = 0; i < 8; i++)
            {
                meshes.Add(ray.Clone(Matrix4x4.RotationZ(MathHelper.DegreesToRadians(angle))));
                angle += angleItem;
            }

            MergeInstance[] instances = new MergeInstance[meshes.Count];
            for (int i = 0; i < instances.Length; i++)
            {
                var inst = new MergeInstance(meshes[i], Matrix4x4.Identity, false);
                instances[i] = inst;
            }

            circle.Merge(instances);
            return BuildSubEntity(owner, name, Colors.White, circle, BoundingVolume.OrientedBox);
        }

    }
}
