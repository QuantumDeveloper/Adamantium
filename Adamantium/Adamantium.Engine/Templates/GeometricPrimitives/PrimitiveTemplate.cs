using System.Threading.Tasks;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Templates;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.GeometricPrimitives
{
    public abstract class PrimitiveTemplate : IEntityTemplate
    {
        protected int Tessellation { get; }
        protected Matrix4x4F? Transform { get; }
        protected bool ToRightHanded { get; }
        protected GeometryType GeometryType { get; }

        protected PrimitiveTemplate(
            GeometryType geometryType,
            int tessellation,
            Matrix4x4F? transform = null,
            bool toRightHanded = false)
        {
            Tessellation = tessellation;
            Transform = transform;
            ToRightHanded = toRightHanded;
            GeometryType = geometryType;
        }

        protected Entity BuildEntityFromPrimitive(
            Entity entity,
            Mesh geometry,
            BoundingVolume volume = BoundingVolume.OrientedBox)
        {
            MeshData meshData = new MeshData();
            meshData.Mesh = geometry;
            FillMetadata(meshData.Metadata);

            Collider collisionComponent;

            switch (volume)
            {
                case BoundingVolume.Sphere:
                    collisionComponent = new SphereCollider();
                    break;
                default:
                    collisionComponent = new BoxCollider();
                    break;
            }

            MeshRenderer renderer = new MeshRenderer();
            entity.AddComponent(meshData);
            entity.AddComponent(collisionComponent);
            entity.AddComponent(renderer);

            return entity;
        }

        protected abstract void FillMetadata(MeshMetadata metadata);

        public abstract Task<Entity> BuildEntity(Entity owner);

    }
}
