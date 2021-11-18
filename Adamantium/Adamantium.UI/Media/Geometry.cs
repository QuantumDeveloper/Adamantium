﻿using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Media
{
   public abstract class Geometry : AdamantiumComponent
   {
      internal Mesh Mesh { get; set; }
      
      internal Mesh StrokeMesh { get; set; }
      
      protected Int32 TesselationFactor { get; set; } = 2;
      
      public Matrix4x4F Transform { get; set; }

      public bool IsClosed { get; set; }

      protected readonly int interrupt = -1;

      internal Geometry()
      {
         Mesh = new Mesh();
         Transformation = Matrix4x4F.Identity;
      }
     
      public abstract Rect Bounds { get; }
      public Matrix4x4F Transformation { get; set; }

      public abstract Geometry Clone();

      public Boolean IsEmpty()
      {
         return Mesh.Positions.Length == 0;
      }
   }
}
