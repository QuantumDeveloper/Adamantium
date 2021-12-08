using System;
using System.Collections.Generic;
using System.ComponentModel;
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
         public Vector2? ZeroLengthDirection;
      }

      private struct DashData
      {
         public List<Vector3F> Points;
         public Vector2 Direction;
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
         if (geometry.StrokeMesh.Points.Length == 0) return;
         
         pen.PenLineJoin = PenLineJoin.Round;
         //@TODO check concave zero length triangulation
         pen.StartLineCap = PenLineCap.ConvexTriangle;
         pen.EndLineCap = PenLineCap.ConvexTriangle;
         var points = geometry.StrokeMesh.Points;

         // for test purposes start
         geometry.IsClosed = true;
         var zeroLineDir = new LineSegment2D(new Vector2(0, 0), new Vector2(1, 0)).DirectionNormalized;
         // for test purposes end

         // sanitize geometry - remove equal points (but not in case when there is a single line with zero length)
         if (points.Length > 2)
         {
            var index = 0;
            var goOn = true;
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
         }

         List<PolygonItem> polygonItems;
         
         if (pen.DashStrokeArray == null || pen.DashStrokeArray.Count == 0)
         {
            polygonItems = GenerateStroke(points, pen, geometry.IsClosed, zeroLineDir);
         }
         else
         {
            polygonItems = GenerateDashes(points, pen, geometry.IsClosed);
         }

         var vertices = new List<Vector3F>();

         foreach (var polygonItem in polygonItems)
         {
            var polygon = new Polygon();
            polygon.AddItem(polygonItem);
            polygon.FillRule = FillRule.NonZero;
            vertices.AddRange(polygon.Fill());
         }
         
         if (vertices.Count > 0) Mesh.SetPoints(vertices).Optimize();
      }

      private List<PolygonItem> GenerateDashes(Vector3F[] points, Pen pen, bool isGeometryClosed)
      {
         var polygonItems = new List<PolygonItem>();
         var dashesData = SplitGeometryToDashes(points, pen, isGeometryClosed);

         foreach (var dashData in dashesData)
         {
            polygonItems.AddRange(GenerateStroke(dashData.Points.ToArray(), pen, false, dashData.Direction));
         }

         return polygonItems;
      }
      
      private double GetGeometryLength(Vector3F[] points, bool isGeometryClosed)
      {
         var length = 0.0;

         for (var i = 0; i < points.Length; i++)
         {
            // loop the cycle
            var next = i + 1;

            if (i == points.Length - 1)
            {
               if (isGeometryClosed) next = 0;
               else break;
            }

            var start = (Vector2)points[i];
            var end = (Vector2)points[next];

            length += (end - start).Length();
         }

         return length;
      }
      
      private List<DashData> SplitGeometryToDashes(Vector3F[] points, Pen pen, bool isGeometryClosed)
      {
         return isGeometryClosed ? SplitClosedGeometryToDashes(points, pen) : SplitOpenedGeometryToDashes(points, pen);
      }

      private List<DashData> SplitOpenedGeometryToDashes(Vector3F[] points, Pen pen)
      {
         var dashesData = new List<DashData>();
         var dashGeometry = new List<Vector3F>();

         var geometryLength = GetGeometryLength(points, false);

         // calculate starting point of visible geometry
         var startPointOffset = pen.DashOffset;
         var dashArrayIndex = startPointOffset > geometryLength ? pen.DashStrokeArray.Count - 1 : 0;
         var isDash = true;
         var dashArrayIndexDiff = startPointOffset > geometryLength ? -1 : 1;
         var dashOffsetSign = startPointOffset > geometryLength ? -1 : 1;

         while (startPointOffset < 0 || startPointOffset > geometryLength)
         {
            // loop dash array index
            if (dashArrayIndex < 0) dashArrayIndex = pen.DashStrokeArray.Count - 1;
            else if (dashArrayIndex >= pen.DashStrokeArray.Count) dashArrayIndex = 0;

            startPointOffset += pen.DashStrokeArray[dashArrayIndex] * dashOffsetSign;

            if (!isDash)
            {
               if (pen.EndLineCap != PenLineCap.Flat) startPointOffset += pen.Thickness / 2.0 * dashOffsetSign;
               if (pen.StartLineCap != PenLineCap.Flat) startPointOffset += pen.Thickness / 2.0 * dashOffsetSign;
            }
            
            if ((pen.DashOffset < 0) || (startPointOffset < 0 || startPointOffset > geometryLength)) dashArrayIndex += dashArrayIndexDiff;
            isDash = !isDash;
         }
         
         var startPoint = GetPointAlongGeometry(points, 0, (Vector2)points[0], startPointOffset, false, out var currentSegmentIndex, out _, out _, out _);

         //var startOfLineReached = startPoint == (Vector2)points[0];
         //var endOfLineReached = startPoint == (Vector2)points[^1];

         var startOfLineReached = false;
         var endOfLineReached = false;
         
         var forwardDashArrayIndex = dashArrayIndex;
         var backwardsDashArrayIndex = dashArrayIndex - 1;
         var isForwardDash = isDash;
         var isBackwardsDash = !isDash;
         var forwardPoint = startPoint;
         var backwardsPoint = startPoint;
         var forwardSegmentIndex = currentSegmentIndex;
         var backwardsSegmentIndex = currentSegmentIndex;

         // spread dashes in both directions from starting point
         while (!startOfLineReached || !endOfLineReached)
         {
            if (!startOfLineReached)
            {
               if (isBackwardsDash) dashGeometry.Add((Vector3F)backwardsPoint);
               
               // loop backwards index
               if (backwardsDashArrayIndex < 0) backwardsDashArrayIndex = pen.DashStrokeArray.Count - 1;
               var backwardsOffset = pen.DashStrokeArray[backwardsDashArrayIndex];
               backwardsDashArrayIndex--;
               
               if (!isBackwardsDash)
               {
                  if (pen.EndLineCap != PenLineCap.Flat) backwardsOffset += pen.Thickness / 2.0;
                  if (pen.StartLineCap != PenLineCap.Flat) backwardsOffset += pen.Thickness / 2.0;
               }

               backwardsPoint = GetPointAlongGeometry(points, backwardsSegmentIndex, backwardsPoint, -backwardsOffset, false, out backwardsSegmentIndex, out var backwardsIntermediatePointIndices, out _, out var backwardsDirection);
               
               startOfLineReached = backwardsPoint == (Vector2)points[0];

               if (isBackwardsDash)
               {
                  dashGeometry.AddRange(backwardsIntermediatePointIndices.Select(index => points[index]));
                  dashGeometry.Add((Vector3F)backwardsPoint);

                  // reverse to preserve right order of start-end line caps for backwards dashes
                  dashGeometry.Reverse();
               
                  var dashData = new DashData()
                  {
                     Points = dashGeometry,
                     Direction = -backwardsDirection
                  };

                  dashesData.Add(dashData);
                  dashGeometry = new List<Vector3F>();
               }
               
               isBackwardsDash = !isBackwardsDash;
            }

            if (!endOfLineReached)
            {
               if (isForwardDash) dashGeometry.Add((Vector3F)forwardPoint);
               
               // loop forward index
               if (forwardDashArrayIndex >= pen.DashStrokeArray.Count) forwardDashArrayIndex = 0;
               var forwardOffset = pen.DashStrokeArray[forwardDashArrayIndex];
               forwardDashArrayIndex++;

               if (!isForwardDash)
               {
                  if (pen.EndLineCap != PenLineCap.Flat) forwardOffset += pen.Thickness / 2.0;
                  if (pen.StartLineCap != PenLineCap.Flat) forwardOffset += pen.Thickness / 2.0;
               }
               
               forwardPoint = GetPointAlongGeometry(points, forwardSegmentIndex, forwardPoint, forwardOffset, false, out forwardSegmentIndex, out var forwardIntermediatePointIndices, out _, out var forwardDirection);
               
               endOfLineReached = forwardPoint == (Vector2)points[^1];

               if (isForwardDash)
               {
                  dashGeometry.AddRange(forwardIntermediatePointIndices.Select(index => points[index]));
                  dashGeometry.Add((Vector3F)forwardPoint);

                  var dashData = new DashData()
                  {
                     Points = dashGeometry,
                     Direction = forwardDirection
                  };

                  dashesData.Add(dashData);
                  dashGeometry = new List<Vector3F>();
               }

               isForwardDash = !isForwardDash;
            }
         }
         
         return dashesData;
      }

      private List<DashData> SplitClosedGeometryToDashes(Vector3F[] points, Pen pen)
      {
         var dashesData = new List<DashData>();
         var offset = pen.DashOffset;
         
         var remainingGeometryLength = GetGeometryLength(points, true);

         // clip the offset if it is too long and is greater then geometry length
         offset %= remainingGeometryLength;

         var dashArrayIndex = 0;
         var isDash = true;
         var dashGeometry = new List<Vector3F>();
         var goOn = true;

         var currentPoint = GetPointAlongGeometry(points, 0, (Vector2)points[0], offset, true, out var currentSegmentIndex, out var intermediatePointIndices, out var pathLength, out var direction);

         while(true)
         {
            // determine if this is dash or space, if dash - add first point of dash
            if (isDash) dashGeometry.Add((Vector3F)currentPoint);
            
            // loop dash array usage
            if (dashArrayIndex == pen.DashStrokeArray.Count) dashArrayIndex = 0;

            // get current offset from template array
            var currentOffset = pen.DashStrokeArray[dashArrayIndex];
            
            // move offset in accordance to line cap endings
            if (!isDash)
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
            
            currentPoint = GetPointAlongGeometry(points, currentSegmentIndex, currentPoint, currentOffset, true, out currentSegmentIndex, out intermediatePointIndices, out pathLength, out direction);

            // add the rest of the dash's points
            if (isDash)
            {
               dashGeometry.AddRange(intermediatePointIndices.Select(index => points[index]));
               dashGeometry.Add((Vector3F)currentPoint);

               var dashData = new DashData()
               {
                  Points = dashGeometry,
                  Direction = direction
               };

               dashesData.Add(dashData);
               dashGeometry = new List<Vector3F>();
            }

            if (!goOn) break;
            
            remainingGeometryLength -= pathLength;
            dashArrayIndex++;
            isDash = !isDash;
         }

         // merge last and first dashes only if geometry is closed and last iteration was dash and not space
         if (isDash)
         {
            var mergedDash = dashesData.Last();
            
            // remove last point of last dash - it is the same as first point of first dash and will be added along with other first dash points
            mergedDash.Points.RemoveAt(mergedDash.Points.Count - 1);
            mergedDash.Points.AddRange(dashesData[0].Points);
            
            // rewrite original first dash
            dashesData[0] = mergedDash;
            
            // remove original last dash
            dashesData.RemoveAt(dashesData.Count - 1);
         }
         
         return dashesData;
      }
      
      private Vector2 GetPointAlongGeometry(Vector3F[] points, int startSegmentIndex, Vector2 startPoint, double offsetFromStartPoint, bool isGeometryClosed, out int segmentIndex, out List<int> intermediatePointIndices, out double pathLength, out Vector2 direction)
      {
         segmentIndex = startSegmentIndex;
         intermediatePointIndices = new List<int>();
         pathLength = 0.0;

         // determine the direction of movement along geometry - positive offset is forward, negative offset is backwards
         var moveForward = offsetFromStartPoint >= 0;
         offsetFromStartPoint = Math.Abs(offsetFromStartPoint);

         // index of start point of segment on which lies the point with desired offset
         var index = startSegmentIndex;
         Vector2 segmentStart;
         Vector2 segmentEnd;

         bool useStartPoint = true;
         
         // determine on which segment lies the point with desired offset
         do
         {
            int nextIndex;
            
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
               
               nextIndex = index + (moveForward ? 1 : -1);
               
               if (nextIndex == points.Length) nextIndex = 0;
               if (nextIndex == -1) nextIndex = points.Length - 1;
            }
            else
            {
               nextIndex = index + 1;

               if (!moveForward)
               {
                  (index, nextIndex) = (nextIndex, index);
               }
            }

            segmentStart = useStartPoint ? startPoint : (Vector2)points[index];
            segmentEnd = (Vector2)points[nextIndex];

            direction = new LineSegment2D((Vector2)points[index], (Vector2)points[nextIndex]).DirectionNormalized;
            
            // if offset is 0 - return start point of geometry
            if (offsetFromStartPoint == 0) return startPoint;
            
            // use start point only at the start of new dash, so use it only once
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

            // if geometry is not closed and we found the end of it - return the last point of geometry
            if (!isGeometryClosed && nextIndex == points.Length - 1)
            {
               return segmentEnd;
            }
            
            // if geometry is not closed and we found the start of it - return the first point of geometry
            if (!isGeometryClosed && nextIndex == 0)
            {
               return segmentEnd;
            }
            
            offsetFromStartPoint = currentOffsetDiff;
            segmentIndex = nextIndex;
            intermediatePointIndices.Add(nextIndex);
            pathLength += currentSegmentLength;
            index += (moveForward ? 1 : -1);
         } while (true);

         return GetPointAlongSegment(segmentStart, segmentEnd, offsetFromStartPoint);
      }

      private Vector2 GetPointAlongSegment(Vector2 start, Vector2 end, double offsetFromStartOfSegment)
      {
         var segment = new LineSegment2D(start, end);

         var direction = segment.DirectionNormalized;

         return start + offsetFromStartOfSegment * direction;
      }

      private List<PolygonItem> GenerateStroke(Vector3F[] points, Pen pen, bool isGeometryClosed, Vector2? zeroLengthLineDirection = null)
      {
         // check if geometry is valid
         if (points.Length < 2) return null;

         var polygonItems = new List<PolygonItem>();
         var strokeSegments = new List<StrokeSegment>();

         // corner case - if line has zero length
         if (points.Length == 2 && points[0] == points[1])
         {
            // if there is no direction for zero length line - set it to default value of (1,0)
            if (zeroLengthLineDirection == null) zeroLengthLineDirection = new Vector2(1, 0);
            var zeroLengthLineDirectionNormalized = zeroLengthLineDirection.Value;
            if (!zeroLengthLineDirectionNormalized.IsNormalized) zeroLengthLineDirectionNormalized.Normalize();

            // compute normal to zero length line based on zero length line direction
            var zeroLengthLineNormal = new Vector2(-zeroLengthLineDirection.Value.Y, zeroLengthLineDirection.Value.X);
            
            // generate top and bottom segments
            var startTopBottomPoints = GenerateTopBottomPoints((Vector2)points[0], pen.Thickness, zeroLengthLineNormal);
            var endTopBottomPoints = GenerateTopBottomPoints((Vector2)points[1], pen.Thickness, zeroLengthLineNormal);

            var strokeSegment = new StrokeSegment
            {
               TopSegment = new LineSegment2D(startTopBottomPoints[0], endTopBottomPoints[0]),
               GeometrySegment = new LineSegment2D((Vector2)points[0], (Vector2)points[1]),
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

               var topBottomSegments = GenerateTopBottomSegments((Vector2)points[i], (Vector2)points[nextIndex], pen.Thickness);

               var strokeSegment = new StrokeSegment
               {
                  TopSegment = topBottomSegments[0],
                  GeometrySegment = new LineSegment2D((Vector2)points[i], (Vector2)points[nextIndex]),
                  BottomSegment = topBottomSegments[1],
                  ZeroLengthDirection = null
               };

               strokeSegments.Add(strokeSegment);
            }
         }
         
         polygonItems.AddRange(GenerateStrokeJoinsAndCaps(strokeSegments, pen, isGeometryClosed));

         return polygonItems;
      }

      private List<Vector2> GenerateCapOutline(PenLineCap capType, Vector2 geometryBasePoint, Vector2 capBasePoint1, Vector2 capBasePoint2, Vector2 capDirection, double thickness)
      {
         var polygonPoints = new List<Vector2>();

         switch (capType)
         {
            case PenLineCap.ConvexTriangle:
            {
               var capPoint0 = geometryBasePoint + thickness / 2.0 * capDirection;

               polygonPoints.Add(capPoint0);

               break;
            }
            case PenLineCap.ConcaveTriangle:
            {
               var capPoint1 = capBasePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = capBasePoint2 + thickness / 2.0 * capDirection;
               
               polygonPoints.Add(capPoint1);
               polygonPoints.Add(geometryBasePoint);
               polygonPoints.Add(capPoint2);

               break;
            }
            case PenLineCap.Square:
            {
               var capPoint1 = capBasePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = capBasePoint2 + thickness / 2.0 * capDirection;

               polygonPoints.Add(capPoint1);
               polygonPoints.Add(capPoint2);

               break;
            }
            case PenLineCap.ConvexRound:
            {
               // var capPoint1 = basePoint1 + thickness * 0.70 * capDirection;
               // var capPoint2 = basePoint2 + thickness * 0.70 * capDirection;
               // var roundPoints = MathHelper.GetCubicBezier(basePoint1, capPoint1, capPoint2, basePoint2, SampleRate);

               var roundPoints = MathHelper.GetArcPoints(capBasePoint1, capBasePoint2, thickness / 2.0, true, ArcSampleRate);

               // remove first and last points, they're equal to base points
               roundPoints.RemoveAt(0);
               roundPoints.RemoveAt(roundPoints.Count - 1);
               
               polygonPoints.AddRange(roundPoints);

               break;
            }
            case PenLineCap.ConcaveRound:
            {
               var capPoint1 = capBasePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = capBasePoint2 + thickness / 2.0 * capDirection;

               //var roundPoints = MathHelper.GetCubicBezier(capPoint1, basePoint1, basePoint2, capPoint2, SampleRate);

               var roundPoints = MathHelper.GetArcPoints(capPoint1, capPoint2, thickness / 2.0, false, ArcSampleRate);

               polygonPoints.AddRange(roundPoints);

               break;
            }
            default:
               return null;
         }

         return polygonPoints;
      }

      /// <summary>
      /// Generates top and bottom stroke outline segments based on geometry segment
      /// </summary>
      /// <param name="startPoint">Start point of geometry segment</param>
      /// <param name="endPoint">End point of geomentry segment</param>
      /// <param name="thickness">Thickness of the stroke</param>
      /// <returns>Array of two stroke outline segments</returns>
      private LineSegment2D[] GenerateTopBottomSegments(Vector2 startPoint, Vector2 endPoint, double thickness)
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
      private Vector2[] GenerateTopBottomPoints(Vector2 point, double thickness, Vector2 normal)
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
      private Vector2 GenerateNormalToSegment(Vector2 startPoint, Vector2 endPoint)
      {
         var dir = endPoint - startPoint;
         var normal = new Vector2(-dir.Y, dir.X);
         normal.Normalize();

         return RoundVector2D(normal, doublePrecision);
      }

      /// <summary>
      /// Rounds Vector2D values with given precision
      /// </summary>
      /// <param name="vector">Vector to be rounded</param>
      /// <param name="precision">Precision to round with</param>
      /// <returns>Rounded vector</returns>
      private Vector2 RoundVector2D(Vector2 vector, int precision)
      {
         return new Vector2(Math.Round(vector.X, precision), Math.Round(vector.Y, precision));
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
      private List<PolygonItem> GenerateStrokeJoinsAndCaps(List<StrokeSegment> strokeSegments, Pen pen, bool isGeometryClosed)
      {
         var polygonItems = new List<PolygonItem>();

         for (var i = 0; i < strokeSegments.Count; i++)
         {
            var polygonPoints = new List<Vector2>();
            var startCapPoints = new List<Vector2>();
            var endCapPoints = new List<Vector2>();

            if (!isGeometryClosed)
            {
               if (i == 0)
               {
                  if (pen.StartLineCap != PenLineCap.Flat)
                  {
                     var geometryBasePoint = strokeSegments[0].GeometrySegment.Start;
                     var capBasePoint1 = strokeSegments[0].BottomSegment.Start;
                     var capBasePoint2 = strokeSegments[0].TopSegment.Start;
                     var capDirection = strokeSegments[0].ZeroLengthDirection != null ? -strokeSegments[0].ZeroLengthDirection.Value : -strokeSegments[0].GeometrySegment.DirectionNormalized;

                     startCapPoints = GenerateCapOutline(pen.StartLineCap, geometryBasePoint, capBasePoint1, capBasePoint2, capDirection, pen.Thickness);
                  }
               }
               
               // not 'else if' here for the case of single segment - we will generate both caps for the same segment 
               if (i == strokeSegments.Count - 1)
               {
                  if (pen.EndLineCap != PenLineCap.Flat)
                  {
                     var geometryBasePoint = strokeSegments[^1].GeometrySegment.End;
                     var capBasePoint1 = strokeSegments[^1].TopSegment.End;
                     var capBasePoint2 = strokeSegments[^1].BottomSegment.End;
                     var capDirection = strokeSegments[^1].ZeroLengthDirection ?? strokeSegments[^1].GeometrySegment.DirectionNormalized;

                     endCapPoints = GenerateCapOutline(pen.EndLineCap, geometryBasePoint, capBasePoint1, capBasePoint2, capDirection, pen.Thickness);
                  }
               }
            }
            
            // compose stroke segment (include caps if they are present)
            polygonPoints.AddRange(startCapPoints);
            polygonPoints.Add(strokeSegments[i].TopSegment.Start);
            polygonPoints.Add(strokeSegments[i].TopSegment.End);
            polygonPoints.AddRange(endCapPoints);
            polygonPoints.Add(strokeSegments[i].BottomSegment.End);
            polygonPoints.Add(strokeSegments[i].BottomSegment.Start);
               
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
            var joinPoints = new List<Vector2>();
            
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
            
            switch (pen.PenLineJoin)
            {
               case PenLineJoin.Bevel:
                  joinPoints.Add(center);
                  joinPoints.Add(left.End);
                  joinPoints.Add(right.Start);
                  break;
               case PenLineJoin.Miter:
               {
                  var intersection = Collision2D.LineLineIntersection(left.Start, left.End, right.Start, right.End);

                  if (intersection != null)
                  {
                     joinPoints.Add(center);
                     joinPoints.Add(left.End);
                     joinPoints.Add(RoundVector2D((Vector2) intersection, doublePrecision));
                     joinPoints.Add(right.Start);
                  }

                  break;
               }
               case PenLineJoin.Round:
               {
                  var intersection = Collision2D.LineLineIntersection(left.Start, left.End, right.Start, right.End);

                  if (intersection != null)
                  {
                     var roundPoints = MathHelper.GetQuadraticBezier(left.End, (Vector2)intersection, right.Start, BezierSampleRate);
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
      
      private void RemoveCollinearSegments(List<Vector2> strokeOutline, bool isClosedGeometry)
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

      public override void RecalculateBounds()
      {
         
      }

      protected internal override void ProcessGeometry()
      {
         throw new NotImplementedException();
      }
   }
}
