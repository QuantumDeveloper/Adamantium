using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models
{
   public class MergeInstance
   {
      public Mesh Mesh { get; set; }

      public Matrix4x4 Transform { get; set; }

      public bool ApplyTransform { get; set; }

      public MergeInstance() { }

      public MergeInstance(Mesh mesh, Matrix4x4 transform, bool applyTransform)
      {
         Mesh = mesh;
         Transform = transform;
         ApplyTransform = applyTransform;
      }
    }
}
