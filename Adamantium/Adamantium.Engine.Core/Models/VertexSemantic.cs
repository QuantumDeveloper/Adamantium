using System;

namespace Adamantium.Engine.Core.Models
{
   [Flags]
   public enum VertexSemantic
   {
      None = 0,
      Position = 2,
      Normal = 4,
      UV0 = 8,
      UV1 = 16,
      UV2 = 32,
      UV3 = 64,
      Color = 128,
      TangentBiNormal = 256,
      JointIndices = 512,
      JointWeights = 1024,
      
   }
}
