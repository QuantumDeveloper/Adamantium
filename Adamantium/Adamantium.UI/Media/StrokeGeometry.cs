using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public class StrokeGeometry : Geometry
   {
      private Rect bounds;
      private int lastIndex = 0;
      private const int interrupt = -1;
      private int doublePrecision = 4;
      public uint BezierSampleRate { get; set; } = 15;
      public double ArcSampleRate { get; set; } = 0.1;

      private struct StrokeSegment
      {
         public LineSegment2D TopSegment;
         public LineSegment2D GeometrySegment;
         public LineSegment2D BottomSegment;
         public Vector2D? ZeroLengthDirection;
      }

      public StrokeGeometry()
      {
      }

      public StrokeGeometry(Pen pen, Geometry geometry)
      {
         GenerateStroke(pen, geometry);
      }

      private void GenerateStroke(Pen pen, Geometry geometry)
      {
         if (geometry.StrokeMesh.Positions.Length == 0) return;
         
         pen.PenLineJoin = PenLineJoin.Round;
         //@TODO check concave zero length triangulation
         pen.StartLineCap = PenLineCap.Flat;
         pen.EndLineCap = PenLineCap.Flat;
         var points = geometry.StrokeMesh.Positions;

         // for test purposes start
         geometry.IsClosed = false;
         var testSegs = new List<Vector3F>();

         testSegs.Add(new Vector3F(100, 100, 0));
         testSegs.Add(new Vector3F(105, 100, 0));
         testSegs.Add(new Vector3F(105, 105, 0));
         

         points = testSegs.ToArray();
         var zeroLineDir = new LineSegment2D(new Vector2D(0, 0), new Vector2D(0.3, 1)).DirectionNormalized;
         // for test purposes end

         // sanitize geometry - remove equal points
         var index = 0;
         bool goOn = true;
         var pointList = points.ToList();
         do
         {
            var nextIndex = index + 1;

            if (index == pointList.Count - 1)
            {
               nextIndex = 0;
               goOn = false;
            }

            if (pointList[index] == pointList[nextIndex]) pointList.RemoveAt(nextIndex);
            else index++;
         } while (goOn);

         points = pointList.ToArray();

         var polygon = new Polygon();
         
         if (pen.DashStrokeArray == null || pen.DashStrokeArray.Count == 0)
         {
            polygon.Polygons.AddRange(GenerateStroke(points, pen, geometry.IsClosed, zeroLineDir));
         }
         else
         {
            //vertices = GenerateDashes(points, pen, 10, geometry.IsClosed);
         }

         if (polygon.Polygons.Count <= 0) return;

         polygon.FillRule = FillRule.NonZero;
         var vertices = polygon.Fill();
         Mesh.SetPositions(vertices).Optimize();
      }

      private List<Vector3F> GenerateDashes(Vector3F[] points, Pen pen, double offset, bool isGeometryClosed)
      {
         var vertices = new List<Vector3F>();
         var dashesGeometry = SplitGeometryToDashes(points, pen, offset, isGeometryClosed);

         foreach (var dashGeometry in dashesGeometry)
         {
            var triangulatedDash = GenerateStroke(dashGeometry.ToArray(), pen, false);
            
            //if (triangulatedDash != null) vertices.AddRange(triangulatedDash);
         }

         return vertices;
      }
      
      private double GetGeometryLength(Vector3F[] points, bool isGeometryClosed)
      {
         var length = 0.0;

         for (var i = 0; i < points.Length; i++)
         {
            var next = i + 1;

            if (i == points.Length - 1)
            {
               if (isGeometryClosed) next = 0;
               else break;
            }

            var start = (Vector2D)points[i];
            var end = (Vector2D)points[next];

            length += (end - start).Length();
         }

         return length;
      }
      
      private List<List<Vector3F>> SplitGeometryToDashes(Vector3F[] points, Pen pen, double offset, bool isGeometryClosed)
      {
         var dashesGeometry = new List<List<Vector3F>>(); 
         
         var remainingGeometryLength = GetGeometryLength(points, isGeometryClosed);

         // clip the offset if it is too long and is greater then geometry length
         offset %= remainingGeometryLength;

         var dashArrayIndex = 0;
         var dashArrayCounter = 0;
         var dashGeometry = new List<Vector3F>();
         bool goOn = true;

         var currentPoint = GetPointAlongGeometry(points, 0, (Vector2D)points[0], offset, isGeometryClosed, out var currentSegmentIndex, out var intermediatePointIndices, out var pathLength);

         do
         {
            // loop dash array usage
            if (dashArrayIndex == pen.DashStrokeArray.Count) dashArrayIndex = 0;
            
            // determine if this is dash or space, if dash - add first point of dash
            if (dashArrayCounter % 2 == 0) dashGeometry.Add((Vector3F)currentPoint);

            var currentOffset = pen.DashStrokeArray[dashArrayIndex];
            
            // move offset in accordance to line cap endings
            if (dashArrayCounter % 2 != 0)
            {
               // if not flat, add prev dash's end cap width
               if (pen.EndLineCap != PenLineCap.Flat) currentOffset += pen.Thickness / 2.0;
               
               // if not flat, add next dash's start cap width
               if (pen.StartLineCap != PenLineCap.Flat) currentOffset += pen.Thickness / 2.0;
            }

            if (currentOffset >= remainingGeometryLength)
            {
               goOn = false;
               currentOffset = remainingGeometryLength;
            }

            currentPoint = GetPointAlongGeometry(points, currentSegmentIndex, currentPoint, currentOffset, isGeometryClosed, out currentSegmentIndex, out intermediatePointIndices, out pathLength);

            if (dashArrayCounter % 2 == 0)
            {
               if (isGeometryClosed)
               {
                  dashGeometry.AddRange(intermediatePointIndices.Select(index => points[index]));
                  dashGeometry.Add((Vector3F)currentPoint);
                  dashesGeometry.Add(dashGeometry);
                  dashGeometry = new List<Vector3F>();
               }
               else
               {
                  foreach (var pointIndex in intermediatePointIndices)
                  {
                     dashGeometry.Add(points[pointIndex]);
                     
                     if (pointIndex == points.Length - 1)
                     {
                        dashesGeometry.Add(dashGeometry);
                        dashGeometry = new List<Vector3F>();
                        dashGeometry.Add(points[0]);
                     }
                  }
                  
                  dashGeometry.Add((Vector3F)currentPoint);
                  dashesGeometry.Add(dashGeometry);
                  dashGeometry = new List<Vector3F>();
               }
            }

            if (!goOn) break;
            
            remainingGeometryLength -= pathLength;
            dashArrayIndex++;
            dashArrayCounter++;
         } while (true);

         // merge last and first dashes only if last dash is not empty space and it's end equals to first dash's start
         if (dashesGeometry[^1][^1] == dashesGeometry[0][0])
         {
            var mergedDash = dashesGeometry.Last();
            
            // remove last point of last dash - it is the same as first point of first dash and will be added along with other first dash points
            mergedDash.RemoveAt(mergedDash.Count - 1);
            mergedDash.AddRange(dashesGeometry[0]);
            
            // rewrite original first dash
            dashesGeometry[0] = mergedDash;
            
            // remove original last dash
            dashesGeometry.RemoveAt(dashesGeometry.Count - 1);
         }
         
         return dashesGeometry;
      }

      private Vector2D GetPointAlongGeometry(Vector3F[] points, int startSegmentIndex, Vector2D startPoint, double offsetFromStartPoint, bool isGeometryClosed, out int segmentIndex, out List<int> intermediatePointIndices, out double pathLength)
      {
         segmentIndex = startSegmentIndex;
         intermediatePointIndices = new List<int>();
         pathLength = 0.0;
         
         // if offset is 0 - return start point of geometry
         if (offsetFromStartPoint == 0) return startPoint;
         
         // determine the direction of movement along geometry - positive offset is forward, negative offset is backwards
         var moveForward = offsetFromStartPoint > 0;
         offsetFromStartPoint = Math.Abs(offsetFromStartPoint);

         // index of start point of segment on which lies the point with desired offset
         var index = startSegmentIndex;
         Vector2D segmentStart;
         Vector2D segmentEnd;

         bool useStartPoint = true;
         
         // determine on which segment lies the point with desired offset
         do
         {
            // loop the cycle according to geometry type and move direction
            if (isGeometryClosed)
            {
               if (moveForward)
               {
                  if (index >= points.Length) index = 0;
               }
               else
               {
                  if (index <= -1) index = points.Length - 1;
               }
            }
            else
            {
               if (moveForward)
               {
                  if (index >= points.Length - 1) index = 0;
               }
               else
               {
                  if (index <= 0) index = points.Length - 1;
               }
            }

            var nextIndex = index + (moveForward ? 1 : -1);

            if (nextIndex == points.Length) nextIndex = 0;
            if (nextIndex == -1) nextIndex = points.Length - 1;

            segmentStart = useStartPoint ? startPoint : (Vector2D)points[index];
            segmentEnd = (Vector2D)points[nextIndex];

            // use start point only at the start of new 'path', so use it only once
            if (useStartPoint) useStartPoint = false;

            var currentSegmentLength = (segmentEnd - segmentStart).Length();
            var currentOffsetDiff = offsetFromStartPoint - currentSegmentLength;

            // offset point is directly in the end of current segment, return the end point
            if (currentOffsetDiff == 0)
            {
               segmentIndex = nextIndex;
               pathLength += currentSegmentLength;
               return segmentEnd;
            }

            // we found the segment on which required point lies, break the cycle
            if (currentOffsetDiff < 0)
            {
               if (!moveForward) segmentIndex = nextIndex;
               pathLength += offsetFromStartPoint;
               break;
            }

            offsetFromStartPoint = currentOffsetDiff;
            segmentIndex = nextIndex;
            intermediatePointIndices.Add(nextIndex);
            pathLength += currentSegmentLength;
            index += (moveForward ? 1 : -1);
         } while (true);

         return GetPointAlongSegment(segmentStart, segmentEnd, offsetFromStartPoint);
      }

      private Vector2D GetPointAlongSegment(Vector2D start, Vector2D end, double offsetFromStartOfSegment)
      {
         var segment = new LineSegment2D(start, end);

         var direction = segment.DirectionNormalized;

         return start + offsetFromStartOfSegment * direction;
      }

      /// <summary>
      /// Generates triangulated stroke and puts it into Mesh for render
      /// </summary>
      /// <param name="points">Geometry points</param>
      /// <param name="pen">Stroke's pen</param>
      /// <param name="isGeometryClosed">Is geometry closed (like triangle, square etc.) or is it just a line</param>
      /// <param name="zeroLengthLineDirection">Direction for the case of zero length line</param>
      private List<PolygonItem> GenerateStroke(Vector3F[] points, Pen pen, bool isGeometryClosed, Vector2D? zeroLengthLineDirection = null)
      {
         // check if geometry is valid
         if (points.Length < 2) return null;

         var polygonItems = new List<PolygonItem>();
         var strokeSegments = new List<StrokeSegment>();

         // corner case - if line has zero length
         if (points.Length == 2 && points[0] == points[1])
         {
            // if there is no direction for zero length line - set it to default value of (1,0)
            if (zeroLengthLineDirection == null) zeroLengthLineDirection = new Vector2D(1, 0);
            var zeroLengthLineDirectionNormalized = zeroLengthLineDirection.Value;
            zeroLengthLineDirectionNormalized.Normalize();

            // compute normal to zero length line based on zero length line direction
            var zeroLengthLineNormal = new Vector2D(-zeroLengthLineDirection.Value.Y, zeroLengthLineDirection.Value.X);
            
            // generate top and bottom segments
            var startTopBottomPoints = GenerateTopBottomPoints((Vector2D)points[0], pen.Thickness, zeroLengthLineNormal);
            var endTopBottomPoints = GenerateTopBottomPoints((Vector2D)points[1], pen.Thickness, zeroLengthLineNormal);

            var strokeSegment = new StrokeSegment
            {
               TopSegment = new LineSegment2D(startTopBottomPoints[0], endTopBottomPoints[0]),
               GeometrySegment = new LineSegment2D((Vector2D)points[0], (Vector2D)points[1]),
               BottomSegment = new LineSegment2D(startTopBottomPoints[1], endTopBottomPoints[1]),
               ZeroLengthDirection = zeroLengthLineDirectionNormalized
            };

            strokeSegments.Add(strokeSegment);
         }
         else
         {
            // generate top and bottom parts of stroke outline
            for (var i = 0; i < points.Length; ++i)
            {
               var nextIndex = i + 1;

               if (i == points.Length - 1)
               {
                  if (!isGeometryClosed) break;

                  // loop cycle around
                  nextIndex = 0;
               }

               var topBottomSegments = GenerateTopBottomSegments((Vector2D)points[i], (Vector2D)points[nextIndex], pen.Thickness);

               var strokeSegment = new StrokeSegment
               {
                  TopSegment = topBottomSegments[0],
                  GeometrySegment = new LineSegment2D((Vector2D)points[i], (Vector2D)points[nextIndex]),
                  BottomSegment = topBottomSegments[1],
                  ZeroLengthDirection = null
               };

               strokeSegments.Add(strokeSegment);
            }

            polygonItems.AddRange(GenerateStrokeJoins(strokeSegments, pen.PenLineJoin, isGeometryClosed));
         }
         
         // generate line caps
         if (!isGeometryClosed)
         {
            // generate start cap
            if (pen.StartLineCap != PenLineCap.Flat)
            {
               var geometryBasePoint = strokeSegments[0].GeometrySegment.Start;
               var capBasePoint1 = strokeSegments[0].TopSegment.Start;
               var capBasePoint2 = strokeSegments[0].BottomSegment.Start;
               var capDirection = strokeSegments[0].ZeroLengthDirection != null ? -strokeSegments[0].ZeroLengthDirection.Value : -strokeSegments[0].GeometrySegment.DirectionNormalized;

               polygonItems.Add(GenerateCap(pen.StartLineCap, geometryBasePoint, capBasePoint1, capBasePoint2, capDirection, pen.Thickness));
            }

            // generate end cap
            if (pen.EndLineCap != PenLineCap.Flat)
            {
               var geometryBasePoint = strokeSegments[^1].GeometrySegment.End;
               var capBasePoint1 = strokeSegments[^1].TopSegment.End;
               var capBasePoint2 = strokeSegments[^1].BottomSegment.End;
               var capDirection = strokeSegments[^1].ZeroLengthDirection ?? strokeSegments[^1].GeometrySegment.DirectionNormalized;

               polygonItems.Add(GenerateCap(pen.EndLineCap, geometryBasePoint, capBasePoint1, capBasePoint2, capDirection, pen.Thickness));
            }
         }

         return polygonItems;
      }

      /// <summary>
      /// Generates outline for stroke cap
      /// </summary>
      /// <param name="capType">Type of the cap</param>
      /// <param name="geometryBasePoint">End point of geometry (according to what we are processing - start cap or end cap)</param>
      /// <param name="capBasePoint1">First of two end points of current stroke's end</param>
      /// <param name="capBasePoint2">Second of two end points of current stroke's end</param>
      /// <param name="capDirection">Direction in which cap should be drawn</param>
      /// <param name="thickness">Thickness of the stroke</param>
      /// <returns>Points of the cap's outline</returns>
      private PolygonItem GenerateCap(PenLineCap capType, Vector2D geometryBasePoint, Vector2D capBasePoint1, Vector2D capBasePoint2, Vector2D capDirection, double thickness)
      {
         var polygonPoints = new List<Vector2D>();

         switch (capType)
         {
            case PenLineCap.ConvexTriangle:
            {
               var capPoint0 = geometryBasePoint + thickness / 2.0 * capDirection;

               polygonPoints.Add(capBasePoint1);
               polygonPoints.Add(capPoint0);
               polygonPoints.Add(capBasePoint2);

               break;
            }
            case PenLineCap.ConcaveTriangle:
            {
               var capPoint1 = capBasePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = capBasePoint2 + thickness / 2.0 * capDirection;
               
               polygonPoints.Add(capBasePoint1);
               polygonPoints.Add(capPoint1);
               polygonPoints.Add(geometryBasePoint);
               polygonPoints.Add(capPoint2);
               polygonPoints.Add(capBasePoint2);

               break;
            }
            case PenLineCap.Square:
            {
               var capPoint1 = capBasePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = capBasePoint2 + thickness / 2.0 * capDirection;

               polygonPoints.Add(capBasePoint1);
               polygonPoints.Add(capPoint1);
               polygonPoints.Add(capPoint2);
               polygonPoints.Add(capBasePoint2);

               break;
            }
            case PenLineCap.ConvexRound:
            {
               // var capPoint1 = basePoint1 + thickness * 0.70 * capDirection;
               // var capPoint2 = basePoint2 + thickness * 0.70 * capDirection;
               // var roundPoints = MathHelper.GetCubicBezier(basePoint1, capPoint1, capPoint2, basePoint2, SampleRate);

               var roundPoints = MathHelper.GetArcPoints(capBasePoint1, capBasePoint2, thickness / 2.0, true, ArcSampleRate);

               polygonPoints.AddRange(roundPoints);

               break;
            }
            case PenLineCap.ConcaveRound:
            {
               var capPoint1 = capBasePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = capBasePoint2 + thickness / 2.0 * capDirection;

               //var roundPoints = MathHelper.GetCubicBezier(capPoint1, basePoint1, basePoint2, capPoint2, SampleRate);

               var roundPoints = MathHelper.GetArcPoints(capPoint1, capPoint2, thickness / 2.0, false, ArcSampleRate);

               polygonPoints.Add(capBasePoint1);
               polygonPoints.AddRange(roundPoints);
               polygonPoints.Add(capBasePoint2);

               break;
            }
            default:
               return null;
         }

         return new PolygonItem(polygonPoints);
      }

      /// <summary>
      /// Generates top and bottom stroke outline segments based on geometry segment
      /// </summary>
      /// <param name="startPoint">Start point of geometry segment</param>
      /// <param name="endPoint">End point of geomentry segment</param>
      /// <param name="thickness">Thickness of the stroke</param>
      /// <returns>Array of two stroke outline segments</returns>
      private LineSegment2D[] GenerateTopBottomSegments(Vector2D startPoint, Vector2D endPoint, double thickness)
      {
         var normal = GenerateNormalToSegment(startPoint, endPoint);

         var startTopBottomPoints = GenerateTopBottomPoints(startPoint, thickness, normal);
         var endTopBottomPoints = GenerateTopBottomPoints(endPoint, thickness, normal);

         var top = new LineSegment2D(startTopBottomPoints[0], endTopBottomPoints[0]);
         var bottom = new LineSegment2D(startTopBottomPoints[1], endTopBottomPoints[1]);

         return new[] { top, bottom };
      }
      
      /// <summary>
      /// Generates top and bottom stroke outline points for given geometry point
      /// </summary>
      /// <param name="point">Geometry point</param>
      /// <param name="thickness">Thickness of the stroke</param>
      /// <param name="normal">Normal to geometry point</param>
      /// <returns>Array of two stroke outline points</returns>
      private Vector2D[] GenerateTopBottomPoints(Vector2D point, double thickness, Vector2D normal)
      {
         return new[]
         {
            RoundVector2D(point - thickness / 2.0 * normal, doublePrecision),
            RoundVector2D(point + thickness / 2.0 * normal, doublePrecision)
         };
      }

      /// <summary>
      /// Generates normal to segment
      /// </summary>
      /// <param name="startPoint">Start point of segment</param>
      /// <param name="endPoint">End point of segment</param>
      /// <returns>Normal to segment</returns>
      private Vector2D GenerateNormalToSegment(Vector2D startPoint, Vector2D endPoint)
      {
         var dir = endPoint - startPoint;
         var normal = new Vector2D(-dir.Y, dir.X);
         normal.Normalize();

         return RoundVector2D(normal, doublePrecision);
      }

      /// <summary>
      /// Rounds Vector2D values with given precision
      /// </summary>
      /// <param name="vector">Vector to be rounded</param>
      /// <param name="precision">Precision to round with</param>
      /// <returns>Rounded vector</returns>
      private Vector2D RoundVector2D(Vector2D vector, int precision)
      {
         return new Vector2D(Math.Round(vector.X, precision), Math.Round(vector.Y, precision));
      }

      /// <summary>
      /// Generates proper join between two stroke outline segments depending on the requested join type
      /// </summary>
      /// <param name="first">First of two consecutive stroke outline segments</param>
      /// <param name="second">Second of two consecutive stroke outline segments</param>
      /// <param name="strokeOutlinePart">One of two stroke outline parts - top or bottom, newly generated points will be added here</param>
      /// <param name="isTopStrokeOutlinePart">Indicates if we are dealing with top stroke outline part</param>
      /// <param name="penLineJoin">Join type</param>
      /// <exception cref="ArgumentException">This exception will be thrown on unhandled join type</exception>
      private List<PolygonItem> GenerateStrokeJoins(List<StrokeSegment> strokeSegments, PenLineJoin penLineJoin, bool isGeometryClosed)
      {
         var polygonItems = new List<PolygonItem>();

         for (var i = 0; i < strokeSegments.Count; i++)
         {
            var polygonPoints = new List<Vector2D>
            {
               strokeSegments[i].TopSegment.Start,
               strokeSegments[i].TopSegment.End,
               strokeSegments[i].BottomSegment.End,
               strokeSegments[i].BottomSegment.Start
            };
            
            polygonItems.Add(new PolygonItem(polygonPoints));

            var nextIndex = i + 1;

            if (i == strokeSegments.Count - 1)
            {
               if (!isGeometryClosed) break;

               // loop cycle around
               nextIndex = 0;
            }

            var angle = MathHelper.AngleBetween(strokeSegments[i].GeometrySegment, strokeSegments[nextIndex].GeometrySegment);

            if (angle == 180) continue;

            var left = new LineSegment2D();
            var right = new LineSegment2D();
            var center = strokeSegments[i].GeometrySegment.End;
            var joinPoints = new List<Vector2D>();
            
            if (angle < 180)
            {
               left = strokeSegments[i].TopSegment;
               right = strokeSegments[nextIndex].TopSegment;
            }
            else if (angle > 180)
            {
               left = strokeSegments[i].BottomSegment;
               right = strokeSegments[nextIndex].BottomSegment;
            }
            
            switch (penLineJoin)
            {
               case PenLineJoin.Bevel:
                  joinPoints.Add(center);
                  joinPoints.Add(left.End);
                  joinPoints.Add(right.Start);
                  break;
               case PenLineJoin.Miter:
               {
                  var intersection = Collision2D.lineLineIntersection(left.Start, left.End, right.Start, right.End);

                  if (intersection != null)
                  {
                     joinPoints.Add(center);
                     joinPoints.Add(left.End);
                     joinPoints.Add(RoundVector2D((Vector2D) intersection, doublePrecision));
                     joinPoints.Add(right.Start);
                  }

                  break;
               }
               case PenLineJoin.Round:
               {
                  var intersection = Collision2D.lineLineIntersection(left.Start, left.End, right.Start, right.End);

                  if (intersection != null)
                  {
                     var roundPoints = MathHelper.GetQuadraticBezier(left.End, (Vector2D)intersection, right.Start, BezierSampleRate);
                     joinPoints.Add(center);
                     joinPoints.AddRange(roundPoints);
                  }

                  break;
               }
               default:
                  throw new ArgumentException("Unhandled stroke join type");
            }
            
            polygonItems.Add(new PolygonItem(joinPoints));
         }

         return polygonItems;
      }
      
      private void RemoveCollinearSegments(List<Vector2D> strokeOutline, bool isClosedGeometry)
      {
         // collinear segments are only possible if there are 3 or more points in outline
         if (strokeOutline.Count < 3) return;
         
         // due to floating point numbers calculation precision issues we will evaluate against small epsilon, not zero further in the algorithm
         var epsilon = 0.001;
         var i = 1;

         do
         {
            if (i == strokeOutline.Count) i = 0;

            var prevIndex = i - 1;
            var nextIndex = i + 1;

            if (i == 0)
            {
               prevIndex = strokeOutline.Count - 1;
            }
            else if (i == strokeOutline.Count - 1)
            {
               if (!isClosedGeometry) break;
               nextIndex = 0;
            }

            // zero length line case
            if (strokeOutline[prevIndex] == strokeOutline[i] ||
                strokeOutline[i] == strokeOutline[nextIndex])
            {
               strokeOutline.RemoveAt(i);
               continue;
            }

            var currentSegment = new LineSegment2D(strokeOutline[prevIndex], strokeOutline[i]);
            var nextSegment = new LineSegment2D(strokeOutline[i], strokeOutline[nextIndex]);
            var angle = MathHelper.AngleBetween(currentSegment, nextSegment);
            
            if (Math.Abs(0 - angle) <= epsilon ||
                Math.Abs(360 - angle) <= epsilon)
            {
               strokeOutline.RemoveAt(i);
               continue;
            }
            
            i++;
         } while (i != 1);
      }

      public override Rect Bounds { get; }
      
      public override Geometry Clone()
      {
         throw new NotImplementedException();
      }
   }
}
