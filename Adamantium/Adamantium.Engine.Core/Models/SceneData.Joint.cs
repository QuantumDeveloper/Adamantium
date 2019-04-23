using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class Joint
      {
         public Joint()
         {
            Children = new List<Joint>();
         }

         public Joint(Joint joint)
         {
            JointName = joint.JointName;
            JointSid = joint.JointSid;
            LocalMatrix = joint.LocalMatrix;
            ParentJoint = joint.ParentJoint;
            Children = new List<Joint>(joint.Children);
            SkeletonId = joint.SkeletonId;
            FullName = joint.FullName;
         }

         public String JointName { get; set; }

         public String JointSid { get; set; }

         public String SkeletonId { get; set; }

         public String FullName { get; set; }

         public Matrix4x4F LocalMatrix { get; set; }

         public Joint ParentJoint { get; set; }

         public List<Joint> Children { get; set; }

      }
   }
}
