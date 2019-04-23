using Adamantium.EntityFramework.Components;

namespace Adamantium.EntityFramework.Extensions
{
   public static class MaterialExtension
   {
      public static Material GetMaterial(this Entity entity)
      {
         return entity.GetComponent<Material>();
      }

      public static void SetTransparency(this Entity entity, float transparency)
      {
         var material = GetMaterial(entity);
         if (material != null)
         {
            material.Transparency = transparency;
         }
      }
   }
}
