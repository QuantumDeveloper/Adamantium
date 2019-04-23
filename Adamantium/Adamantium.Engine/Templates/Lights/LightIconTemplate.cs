using System;
using Adamantium.Engine.Core.Models;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Lights
{
    public abstract class LightIconTemplate
    {
        public abstract Entity BuildEntity(Entity owner, string name);

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
