using Adamantium.Mathematics;

namespace Adamantium.UI
{
   public static class Collision
   {
      public static Ray CalculateRay(Size windowSize, Vector2F mouseLocation, Matrix4x4F worldviewProj)
      {
         return Ray.GetPickRay((int)mouseLocation.X, (int)mouseLocation.Y,
            new ViewportF(0, 0, (float)windowSize.Width, (float)windowSize.Height, 0, 1), worldviewProj);
      }

      public static Ray CalculateRay(Vector2F mouseLocation, Matrix4x4F worldviewProj, ViewportF viewport)
      {
         return Ray.GetPickRay((int)mouseLocation.X, (int)mouseLocation.Y, viewport, worldviewProj);
      }
   }
}
