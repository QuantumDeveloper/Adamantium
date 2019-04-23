﻿using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class Geometry
      {
         public Geometry()
         {
            Positions = new List<Vector3F>();
            UV0 = new List<Vector2F>();
            UV1 = new List<Vector2F>();
            UV2 = new List<Vector2F>();
            UV3 = new List<Vector2F>();
            Normals = new List<Vector3F>();
            IndexBuffer = new List<int>();
            Tangents = new List<Vector3F>();
            Bitangents = new List<Vector3F>();
            Colors = new List<Color>();
            JointIndices = new List<Vector4F>();
            JointWeights = new List<Vector4F>();
         }

         public VertexSemantic Semantic { get; set; }

         public List<Int32> IndexBuffer { get; set; }

         public List<Vector3F> Positions { get; set; }

         public List<Vector2F> UV0 { get; set; }
         public List<Vector2F> UV1 { get; set; }
         public List<Vector2F> UV2 { get; set; }
         public List<Vector2F> UV3 { get; set; }

         public List<Color> Colors { get; set; }

         public List<Vector3F> Normals { get; set; }

         public List<Vector3F> Tangents { get; set; }

         public List<Vector3F> Bitangents { get; set; }

         public List<Vector4F> JointIndices { get; set; } 

         public List<Vector4F> JointWeights { get; set; } 

         public Mathematics.OrientedBoundingBox OrientedBoundingBox { get; set; }

         public String MaterialId { get; set; }

         public PrimitiveTopology MeshTopology { get; set; }
      }
   }
}
