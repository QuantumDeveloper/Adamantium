using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media
{
   public abstract class Geometry : AdamantiumComponent
   {
      internal Mesh Mesh { get; set; }
      
      internal Mesh StrokeMesh { get; set; }
      
      protected Int32 TesselationFactor { get; set; } = 20;

      public static readonly AdamantiumProperty TransformProperty = AdamantiumProperty.Register(nameof(Transform),
         typeof(Transform), 
         typeof(Geometry),
         new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, TransformChanged));

      private static void TransformChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
      {
         
      }

      public Transform Transform
      {
         get => GetValue<Transform>(TransformProperty);
         set => SetValue(TransformProperty, value);
      }

      public bool IsClosed { get; set; }

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
