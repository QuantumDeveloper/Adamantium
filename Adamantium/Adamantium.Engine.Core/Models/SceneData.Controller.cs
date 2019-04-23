using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class Controller
      {
         public Controller()
         {
            BindShapeMatrix = Matrix4x4F.Identity;
            ControllerId = String.Empty;
            Name = String.Empty;
            MeshId = String.Empty;
            JointDictionary = new Dictionary<String, Matrix4x4F>();
            JointNames = new List<String>();
            JointMatrices = new List<Matrix4x4F>();
            BoneIndices = new List<Vector4F>();
            BoneWeights = new List<Vector4F>();
         }

         public Matrix4x4F BindShapeMatrix { get; set; }

         public String ControllerId { get; set; }

         public String MeshId { get; set; }

         public String Name { get; set; }

         public Dictionary<String, Matrix4x4F> JointDictionary { get; set; }

         public List<Vector4F> BoneIndices { get; set; }
         
         public List<Vector4F> BoneWeights { get; set; } 

         public List<String> JointNames { get; set; }

         public List<Matrix4x4F> JointMatrices { get; set; }

      }
   }
}
