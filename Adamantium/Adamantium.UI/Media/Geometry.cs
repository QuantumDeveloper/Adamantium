using System;
using Adamantium.Engine.Core.Models;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media
{
   public abstract class Geometry : AdamantiumComponent
   {
      internal Mesh Mesh { get; set; }
      
      internal Mesh StrokeMesh { get; set; }
      
      protected Int32 TesselationFactor { get; set; }

      public static readonly AdamantiumProperty TransformProperty = AdamantiumProperty.Register(nameof(Transform),
         typeof(Transform), 
         typeof(Geometry),
         new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

      public Transform Transform
      {
         get => GetValue<Transform>(TransformProperty);
         set => SetValue(TransformProperty, value);
      }

      public bool IsClosed { get; set; }

      protected Geometry()
      {
         Mesh = new Mesh();
         StrokeMesh = new Mesh();
         TesselationFactor = 20;
      }
     
      public abstract Rect Bounds { get; }

      public abstract Geometry Clone();

      public Boolean IsEmpty()
      {
         return Mesh.Points.Length == 0;
      }

      public abstract void RecalculateBounds();

      protected internal abstract void ProcessGeometry();

      protected override void OnPropertyChanged(AdamantiumPropertyChangedEventArgs e)
      {
         base.OnPropertyChanged(e);
         RecalculateBounds();
      }
   }
}
