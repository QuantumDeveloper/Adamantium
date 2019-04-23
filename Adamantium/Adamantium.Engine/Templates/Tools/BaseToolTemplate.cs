using System;
using Adamantium.Engine.Core.Models;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Tools
{
   public abstract class BaseToolTemplate
   {
      protected Vector3F baseScale;

      protected Entity BuildSubEntity(Entity owner, String name, Mesh geometry, Color color, BoundingVolume volume = BoundingVolume.None, float transparency = 1.0f)
      {
         MeshData meshData = new MeshData();
         meshData.Mesh = geometry;
         
         Material material = new Material();
         material.MeshColor = color.ToVector3();
         material.HighlightColor = Colors.Yellow.ToVector4();
         material.Transparency = transparency;
         
         var renderer = new MeshRenderer();

         var entity = new Entity(owner, name);

         entity.Transform.BaseScale = baseScale;
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

         entity.AddComponent(renderer);

         return entity;
      }

       public abstract Entity BuildEntity(Entity owner, string name);
   }
}
