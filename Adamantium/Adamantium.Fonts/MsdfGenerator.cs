using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public class MsdfGenerator
    {
        private List<LineSegment2D> segments;

        public MsdfGenerator()
        {
        }

        public MsdfGenerator(List<LineSegment2D> segmentData)
        {
            segments = segmentData;
        }

        public void SetSegmentData(List<LineSegment2D> segmentData)
        {
            segments = segmentData;
        }
        
        // --- PREPROCESSORS ---
        private List<List<LineSegment2D>> SplitToRawContours()
        {
            var res = new List<List<LineSegment2D>>();
            var contour = new List<LineSegment2D>();

            for (var i = 0; i < (segments.Count - 1); ++i)
            {
                var currentSegment = segments[i];
                var nextSegment = segments[i + 1];
                
                contour.Add(currentSegment);

                if (!IsSegmentsConnected(ref currentSegment, ref nextSegment))
                {
                    res.Add(contour);
                    contour = new List<LineSegment2D>();
                }

                if (i == (segments.Count - 2))
                {
                    contour.Add(nextSegment);
                    res.Add(contour);
                }
            }

            return res;
        }

        private bool FindFirstSharpAngle(List<LineSegment2D> contour, int angleThreshold, out int startIndex)
        {
            for (int i = startIndex = 0; i < contour.Count; i++)
            {
                var currentSeg = contour[i];

                if (i == (contour.Count - 1))
                {
                    startIndex = 0;
                }
                else
                {
                    startIndex = i + 1;
                }

                var nextSeg = contour[startIndex];

                if (GetSegmentsAngle(currentSeg, nextSeg) < angleThreshold)
                {
                    return true;
                }
            }

            return false;
        }
        
        private List<Contour> SplitToEdgedContours()
        {
            var angleThreshold = 135;

            var res = new List<Contour>(); 
            var rawContours = SplitToRawContours();

            foreach (var contour in rawContours)
            {
                var edgedContour = new Contour();
                var startIndex = 0;

                if (!FindFirstSharpAngle(contour, angleThreshold, out startIndex))
                {
                    var currentEdge = new Edge();
                    currentEdge.Segments = contour;
                    edgedContour.Edges.Add(currentEdge);
                }
                else
                {
                    var currentEdge = new Edge();
                    var index = startIndex;
                    var cnt = 0;
                    while (cnt < contour.Count)
                    {
                        var currentSeg = contour[index];
                        currentEdge.Segments.Add(currentSeg);
                        index++;

                        if (index == contour.Count)
                        {
                            index = 0;
                        }

                        var nextSeg = contour[index];

                        if (GetSegmentsAngle(currentSeg, nextSeg) < angleThreshold)
                        {
                            edgedContour.Edges.Add(currentEdge);
                            currentEdge = new Edge();
                        }
                        
                        cnt++;
                    }
                }
                
                res.Add(edgedContour);
            }

            return res;
        }
        
        // --- MAIN FUNCS ---
        // edge is a list of connected segments which have no sharp corners within them
        private void ColorEdges()
        {
            var segmentLengthThreshold = 10;
            Color currentColor;
            var contours = SplitToEdgedContours();
            
            segments.Clear();

            foreach (var contour in contours)
            {
                currentColor = Color.FromRgba(255, contour.Edges.Count == 1 ? (byte) 255 : (byte) 0, 255, 255);

                foreach (var edge in contour.Edges)
                {
                    for (int i = 0; i < edge.Segments.Count; i++)
                    {
                        var currentSeg = edge.Segments[i];

                        /*if (currentSeg.Direction.Length() < segmentLengthThreshold)
                        {
                            currentSeg.MsdfColor = Colors.White;    
                        }
                        else
                        {*/
                        currentSeg.MsdfColor = currentColor;
                        /*}*/

                        segments.Add(currentSeg);
                    }
                    
                    if (currentColor == Color.FromRgba(255, 255, 0, 255))
                    {
                        currentColor = Color.FromRgba(0, 255, 255, 255);
                    }
                    else
                    {
                        currentColor = Color.FromRgba(255, 255, 0, 255);
                    }
                }
            }
        }
        
        private void GetColoredDistances(Vector2D point, out double redDistance, out double greenDistance, out double blueDistance)
        {
            double closestRedDistance = Double.MaxValue;
            double closestGreenDistance = Double.MaxValue;
            double closestBlueDistance = Double.MaxValue;

            // there can be up to two closest segments in case if point is close to segments' connection
            // we will store both and then determine the signed pseudo-distance
            // if these two signed pseudo-distances have different signs, use the one with negative, because the point is outside
            var closestRedSegments = new List<LineSegment2D>();
            var closestGreenSegments = new List<LineSegment2D>();
            var closestBlueSegments = new List<LineSegment2D>();

            foreach (var segment in segments)
            {
                if (segment.Start == segment.End)
                {
                    throw new Exception("START == END");
                }
                
                var distance = GetDistanceToSegment(segment, point);

                if (ApplyColorMask(segment.MsdfColor, true, false, false) != Colors.Black
                    && distance <= closestRedDistance)
                {
                    if (distance < closestRedDistance)
                    {
                        closestRedSegments.Clear();
                        closestRedDistance = distance;
                    }
                    
                    closestRedSegments.Add(segment);
                }
                
                if (ApplyColorMask(segment.MsdfColor, false, true, false) != Colors.Black
                    && distance <= closestGreenDistance)
                {
                    if (distance < closestGreenDistance)
                    {
                        closestGreenSegments.Clear();
                        closestGreenDistance = distance;
                    }
                    
                    closestGreenSegments.Add(segment);
                }
                
                if (ApplyColorMask(segment.MsdfColor, false, false, true) != Colors.Black
                    && distance <= closestBlueDistance)
                {
                    if (distance < closestBlueDistance)
                    {
                        closestBlueSegments.Clear();
                        closestBlueDistance = distance;
                    }
                    
                    closestBlueSegments.Add(segment);
                }
            }

            redDistance = GetSignedPseudoDistanceToSegmentsJoint(closestRedSegments, point);
            greenDistance = GetSignedPseudoDistanceToSegmentsJoint(closestGreenSegments, point);
            blueDistance = GetSignedPseudoDistanceToSegmentsJoint(closestBlueSegments, point);
        }
        
        public Color[] GenerateDirectMSDF(uint size, Rectangle glyphBoundingRectangle)
        {
            // 1. Color all segments
            ColorEdges();
            
            // 2. Calculate boundaries for original glyph
            float glyphSize = Math.Max(glyphBoundingRectangle.Width, glyphBoundingRectangle.Height);
            glyphSize += glyphSize * 0.3f;
            var originalDimensions = new Vector2D(glyphSize);
            
            // 3. Place glyph in center of calculated boundaries
            var glyphCenter = glyphBoundingRectangle.Center;
            var originalCenter = originalDimensions / 2;
            var diff = originalCenter - glyphCenter;
            
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                var start = segment.Start;
                var end = segment.End;
                var msdfColor = segment.MsdfColor;
                
                segment = new LineSegment2D(start + diff, end + diff);
                segment.MsdfColor = msdfColor;
                
                segments[index] = segment;
            }

            // 4. Generate colored pseudo-distance field
            var coloredDistances = new ColoredDistance[size, size];

            double minDistance = Double.MaxValue;
            double maxDistance = Double.MinValue;

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    // determine the closest segment to current sampling point
                    var samplingPoint = new Vector2D(originalDimensions.X / size * (x + 0.5), originalDimensions.Y - (originalDimensions.Y / size * (y + 0.5)));

                    GetColoredDistances(samplingPoint, out coloredDistances[x, y].redDistance, out coloredDistances[x, y].greenDistance, out coloredDistances[x, y].blueDistance);
                    
                    minDistance = Math.Min(minDistance, MinOfThree(coloredDistances[x, y].redDistance, coloredDistances[x, y].greenDistance, coloredDistances[x, y].blueDistance));
                    maxDistance = Math.Max(maxDistance, MaxOfThree(coloredDistances[x, y].redDistance, coloredDistances[x, y].greenDistance, coloredDistances[x, y].blueDistance));
                }
            }
            
            // 5. Normalize colored pseudo-distance field to [0 .. 255] range
            var msdf = new List<Color>();

            maxDistance = Math.Max(Math.Abs(minDistance), maxDistance);
            /*maxDistance = maxDistance / 3;
            minDistance = -maxDistance;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (coloredDistances[x, y].redDistance < minDistance)
                    {
                        coloredDistances[x, y].redDistance = minDistance;
                    }
                    
                    if (coloredDistances[x, y].redDistance > maxDistance)
                    {
                        coloredDistances[x, y].redDistance = maxDistance;
                    }
                    
                    if (coloredDistances[x, y].greenDistance < minDistance)
                    {
                        coloredDistances[x, y].greenDistance = minDistance;
                    }
                    
                    if (coloredDistances[x, y].greenDistance > maxDistance)
                    {
                        coloredDistances[x, y].greenDistance = maxDistance;
                    }
                    
                    if (coloredDistances[x, y].blueDistance < minDistance)
                    {
                        coloredDistances[x, y].blueDistance = minDistance;
                    }
                    
                    if (coloredDistances[x, y].blueDistance > maxDistance)
                    {
                        coloredDistances[x, y].blueDistance = maxDistance;
                    }
                }
            }*/

            FixArtefacts(coloredDistances, size);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    coloredDistances[x, y].redDistance = GetDistanceColor(coloredDistances[x, y].redDistance, maxDistance);
                    coloredDistances[x, y].greenDistance = GetDistanceColor(coloredDistances[x, y].greenDistance, maxDistance);
                    coloredDistances[x, y].blueDistance = GetDistanceColor(coloredDistances[x, y].blueDistance, maxDistance);

                    var color = Color.FromRgba((byte)coloredDistances[x, y].redDistance, (byte)coloredDistances[x, y].greenDistance, (byte)coloredDistances[x, y].blueDistance, 255);
                    
                    msdf.Add(color);
                }
            }

            return msdf.ToArray();
        }

        // --- ARTEFACT FIXING ---
        // true - no collision, false - collision
        private bool CheckNeighbor(ColoredDistance neighbor, ColoredDistance current)
        {
            var threshold = 50;

            int cnt = 0;
            
            bool isNeighborRedPositive = (neighbor.redDistance >= 0);
            bool isNeighborGreenPositive = (neighbor.greenDistance >= 0);
            bool isNeighborBluePositive = (neighbor.blueDistance >= 0);

            bool isCurrentRedPositive = (current.redDistance >= 0);
            bool isCurrentGreenPositive = (current.greenDistance >= 0);
            bool isCurrentBluePositive = (current.blueDistance >= 0);

            if (isNeighborRedPositive ^ isCurrentRedPositive && 
                Math.Abs(neighbor.redDistance - current.redDistance) > threshold)
            {
                cnt++;
            }
            
            if (isNeighborGreenPositive ^ isCurrentGreenPositive && 
                Math.Abs(neighbor.greenDistance - current.greenDistance) > threshold)
            {
                cnt++;
            }
            
            if (isNeighborBluePositive ^ isCurrentBluePositive && 
                Math.Abs(neighbor.blueDistance - current.blueDistance) > threshold)
            {
                cnt++;
            }

            return (cnt < 2);
        }
        
        // true - no collision, false - collision
        private bool CheckForCollision(List<ColoredDistance> neighbors, ColoredDistance current)
        {
            
            foreach (var neighbor in neighbors)
            {
                if (!CheckNeighbor(neighbor, current))
                {
                    return false;
                }
            }

            return true;
        }

        private void FixArtefacts(ColoredDistance[,] data, uint size)
        {
            var correctionList = new List<CorrectionLocation>();

            for (int y = 1; y < size - 1; y++)
            {
                for (int x = 1; x < size - 1; x++)
                {
                    var neighbors = new List<ColoredDistance>();

                    // get 8 neighbors of the current pixel
                    neighbors.Add(data[x - 1, y - 1]);
                    neighbors.Add(data[x, y - 1]);
                    neighbors.Add(data[x + 1, y - 1]);
                    
                    neighbors.Add(data[x - 1, y]);
                    var current = data[x, y];
                    neighbors.Add(data[x + 1, y]);
                    
                    neighbors.Add(data[x - 1, y + 1]);
                    neighbors.Add(data[x, y + 1]);
                    neighbors.Add(data[x + 1, y + 1]);

                    if (!CheckForCollision(neighbors, current))
                    {
                        var correction = new CorrectionLocation();
                        correction.X = x;
                        correction.Y = y;
                        
                        correctionList.Add(correction);
                    }
                }
            }

            foreach (var correction in correctionList)
            {
                var pixel = data[correction.X, correction.Y];
                var median = Median(pixel.redDistance, pixel.greenDistance, pixel.blueDistance);

                pixel.redDistance = pixel.greenDistance = pixel.blueDistance = median;

                data[correction.X, correction.Y] = pixel;
            }
        }
        
        // --- HELPERS ---
        private bool IsSegmentsConnected(ref LineSegment2D segment1, ref LineSegment2D segment2)
        {
            return segment1.End == segment2.Start;
        }
        
        private double GetSegmentsAngle(LineSegment2D current, LineSegment2D next)
        {
            var newSeg = new LineSegment2D(current.End, current.Start);

            return MathHelper.DetermineAngleInDegrees(newSeg.Start, newSeg.End, next.Start, next.End);
        }
        
        private double Median(double a, double b, double c)
        {
            return Math.Max(Math.Min(a,b), Math.Min(Math.Max(a,b), c));
        }
        
        private byte GetDistanceColor(double distance, double maxDistance)
        {
            double range = 2 * maxDistance;

            double color = (distance / range + 0.5) * 255;

            return (byte)color;
        }
        
        private double MinOfThree(double d1, double d2, double d3)
        {
            return Math.Min(d1, Math.Min(d2, d3));
        }
        
        private double MaxOfThree(double d1, double d2, double d3)
        {
            return Math.Max(d1, Math.Max(d2, d3));
        }
        
        private double GetDistanceToSegment(LineSegment2D segment, Vector2D point)
        {
            return point.DistanceToPoint(segment.Start, segment.End);
        }

        private double GetSignedPseudoDistanceToSegment(LineSegment2D segment, Vector2D point)
        {
            var distance = MathHelper.PointToLineDistance(segment.Start, segment.End, point);

            var startToPointVector = new LineSegment2D(segment.Start, point);

            var cross = MathHelper.Cross2D(segment.Direction, startToPointVector.Direction);

            if (cross < 0)
            {
                distance = -distance;
            }
            
            return distance;
        }
        
        private double GetSignedPseudoDistanceToSegmentsJoint(List<LineSegment2D> closestSegments, Vector2D point)
        {
            var signedDistances = new List<double>();
            
            if (closestSegments.Count > 2)
            {
                throw new Exception("COUNT > 2");
            }
            
            foreach (var segment in closestSegments)
            {
                signedDistances.Add(GetSignedPseudoDistanceToSegment(segment, point));
            }

            return signedDistances.Min();
        }
        
        private Color ApplyColorMask(Color color, bool redMask, bool greenMask, bool blueMask)
        {
            color.R *= redMask ? (byte)1 : (byte)0;
            color.G *= greenMask ? (byte)1 : (byte)0;
            color.B *= blueMask ? (byte)1 : (byte)0;

            return color;
        }
        
        // --- ADDITIONAL DATA TYPES ---
        private struct ColoredDistance
        {
            public double redDistance;
            public double greenDistance;
            public double blueDistance;
        }
        
        private struct CorrectionLocation
        {
            public int X;
            public int Y;
        }
        
        private class Contour
        {
            public List<Edge> Edges;

            public Contour()
            {
                Edges = new List<Edge>();
            }
        }
        
        private class Edge
        {
            public List<LineSegment2D> Segments;

            public Edge()
            {
                Segments = new List<LineSegment2D>();
            }
        }
        
        // --- TEST HELPERS ---
        public Mesh GetColoredPoints()
        {
            ColorEdges();

            var mesh = new Mesh();

            var positions = new List<Vector2D>();
            var colors = new List<Color>();
            
            foreach (var segment in segments)
            {
                positions.Add(segment.Start);
                positions.Add(segment.End);
                colors.Add(segment.MsdfColor);
                colors.Add(segment.MsdfColor);
            }

            mesh.SetPositions(positions);
            mesh.SetColors(colors);

            return mesh;
        }
    }
}