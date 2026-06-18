using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;

namespace Adamantium.UI.Core.Media;

public class LineGeometry : Geometry
{
   private Rect bounds;

   public static readonly AdamantiumProperty StartPointProperty =
      AdamantiumProperty.Register(nameof(StartPoint), typeof(Vector2), typeof(LineGeometry),
         new PropertyMetadata(new Vector2(0), PropertyMetadataOptions.AffectsMeasure));
      
   public static readonly AdamantiumProperty EndPointProperty =
      AdamantiumProperty.Register(nameof(EndPoint), typeof(Vector2), typeof(LineGeometry),
         new PropertyMetadata(new Vector2(0), PropertyMetadataOptions.AffectsMeasure));

   public override Rect Bounds => bounds;

   public Vector2 StartPoint
   {
      get => GetValue<Vector2>(StartPointProperty);
      set => SetValue(StartPointProperty, value);
   }

   public Vector2 EndPoint
   {
      get => GetValue<Vector2>(EndPointProperty);
      set => SetValue(EndPointProperty, value);
   }

   public LineGeometry()
   {
   }

   static LineGeometry()
   {
      IsClosedProperty.OverrideMetadata(typeof(LineGeometry), new PropertyMetadata(false));
   }

   public LineGeometry(Vector2 startPoint, Vector2 endPoint) : this()
   {
      StartPoint = startPoint;
      EndPoint = endPoint;
      bounds = new Rect(startPoint, endPoint);
   }

   public override void RecalculateBounds()
   {
      // Derive bounds from the actual end points, not the mesh: the mesh is empty until ProcessGeometry runs,
      // and bounds are queried earlier (e.g. Shape.MeasureOverride calls Data.RecalculateBounds()).
      bounds = Rect.FromPoints([StartPoint, EndPoint]);
   }
      
   public override Geometry Clone()
   {
      throw new NotImplementedException();
   }

   protected internal override void ProcessGeometryCore(GeometryType geometryType)
   {
      CreateLine();
   }

   private void CreateLine()
   {
      Mesh.AddContour(new [] { (Vector3)StartPoint, (Vector3)EndPoint }, false);
   }
}