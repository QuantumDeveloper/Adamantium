using Adamantium.Core.TypeParsing;
using Adamantium.Graphics.Core.Models;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Media;

[TypeParser(typeof(GeometryParser))]
public abstract class Geometry : AdamantiumComponent
{
   public bool IsProcessed { get; private set; }
      
   public Mesh Mesh { get; set; }
      
   protected Int32 TesselationFactor { get; set; }

   public static readonly AdamantiumProperty TransformProperty = AdamantiumProperty.Register(nameof(Transform),
      typeof(Transform), 
      typeof(Geometry),
      new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, TransformChangedCallback));

   public static readonly AdamantiumProperty IsClosedProperty =
      AdamantiumProperty.Register(nameof(IsClosed), typeof(bool), typeof(Geometry), new PropertyMetadata(true));

   private static void TransformChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is Geometry geometry)
      {
         if (e.OldValue is Transform transform)
         {
            transform.PropertyChanged -= geometry.TransformOnPropertyChanged;
         }

         if (e.NewValue is Transform transform1)
         {
            transform1.PropertyChanged += geometry.TransformOnPropertyChanged;
         }
      }
   }

   private void TransformOnPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
   {
      InvalidateGeometry();
      RaiseComponentUpdated();
   }

   public Transform Transform
   {
      get => GetValue<Transform>(TransformProperty);
      set => SetValue(TransformProperty, value);
   }

   public bool IsClosed
   {
      get => GetValue<bool>(IsClosedProperty);
      set => SetValue(IsClosedProperty, value);
   }

   protected Geometry()
   {
      Mesh = new Mesh();
      TesselationFactor = 20;
      IsClosed = true;
   }
     
   public abstract Rect Bounds { get; }

   public abstract Geometry Clone();

   public Boolean IsEmpty()
   {
      return Mesh.Points.Length == 0;
   }

   public abstract void RecalculateBounds();

   public void ProcessGeometry(GeometryType geometryType)
   {
      if (!IsProcessed)
      {
         ProcessGeometryCore(geometryType);
         if (Transform != null)
         {
            var matrix = Transform.Matrix;
            Mesh.ApplyTransform(matrix);
         }
         RecalculateBounds();
         IsProcessed = true;
      }
   }

   public void InvalidateGeometry()
   {
      IsProcessed = false;
   }
   
   protected internal abstract void ProcessGeometryCore(GeometryType geometryType);

   protected override void OnComponentUpdated()
   {
      base.OnComponentUpdated();
      InvalidateGeometry();
   }

   public static CombinedGeometry Combine(Geometry geometry1, Geometry geometry2, GeometryCombineMode combineMode, Transform transform = null)
   {
      return new CombinedGeometry(){Geometry1 = geometry1, Geometry2 = geometry2, GeometryCombineMode = combineMode, Transform = transform};
   }
}