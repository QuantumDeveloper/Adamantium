using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Exceptions;
using Adamantium.Fonts.Parsers.CFF;
using Adamantium.Fonts.Tables.CFF;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public class Glyph
    {
        private HashSet<UInt32> uniqueUnicodes;
        private List<UInt32> unicodes;

        private List<Outline> outlines;
        private bool isSplitOnSegments;

        private List<Command> commandList;
        private List<LineSegment2D> segments;
        
        internal IReadOnlyCollection<Outline> Outlines => outlines.AsReadOnly();
        private readonly Dictionary<uint, SampledOutline[]> sampledOutlinesCache;
        
        public uint Index { get; }
        
        public bool IsEmpty { get; }
        public uint Unicode => unicodes.FirstOrDefault();

        public IReadOnlyCollection<UInt32> Unicodes => unicodes.AsReadOnly();
        
        public string Name { get; internal set; }
        
        internal UInt32 SID { get; set; }
        
        public bool IsInvalid { get; set; } // set this flag if glyph was not properly loaded

        public ushort AdvanceWidth { get; internal set; }
        
        public ushort AdvanceHeight { get; internal set; }
        
        public short LeftSideBearing { get; internal set; }
        
        public short TopSideBearing { get; internal set; }
        
        public bool IsComposite { get; internal set; } // For TTF
        
        public Rectangle BoundingRectangle { get; internal set; }
        
        public OutlineType OutlineType { get; internal set; }
        
        public GlyphClassDefinition ClassDefinition { get; internal set; }

        internal List<CompositeGlyphComponent> CompositeGlyphComponents;
        
        public GlyphLayoutData Layout { get; internal set; }

        public bool HasOutlines => outlines.Count > 0;
        
        internal byte[] Instructions { get; private set; }
        
        internal static Glyph EmptyGlyph(uint index)
        {
            return new Glyph(index, true);
        }

        public Glyph(uint index)
        {
            Index = index;
            Name = String.Empty;
            outlines = new List<Outline>();
            sampledOutlinesCache = new Dictionary<uint, SampledOutline[]>();
            unicodes = new List<uint>();
            uniqueUnicodes = new HashSet<uint>();
            CompositeGlyphComponents = new List<CompositeGlyphComponent>();
        }

        private Glyph(uint index, bool isEmpty) : this(index)
        {
            IsEmpty = isEmpty;
            Name = String.Empty;
        }

        public Vector3F[] Sample(byte rate)
        {
            if (IsEmpty)
            {
                return Array.Empty<Vector3F>();
            }
            
            if (rate == 0) rate = 1;

            if (!isSplitOnSegments)
            {
                SplitOnSegments();
            }

            // if (sampledOutlinesCache.TryGetValue(rate, out var sampledOutlines))
            // {
            //     return sampledOutlines;
            // }
            
            var sampledOutlines = this.GenerateOutlines(rate);
            sampledOutlinesCache[rate] = sampledOutlines;
            var points = RemoveSelfIntersections(sampledOutlines);
            
            return points;
        }

        private void SplitOnSegments()
        {
            if (OutlineType == OutlineType.TrueType)
            {
                SplitOnSegmentsTTF();
            }
            else
            {
                SplitOnSegmentsCFF();
            }

            isSplitOnSegments = true;
        }

        private void SplitOnSegmentsTTF()
        {
            foreach (var outline in Outlines)
            {
                var segment = new OutlineSegment();
                
                // outline should start from non-control point
                if (outline.Points[0].IsControl)
                {
                    segment.AddPoint(outline.Points.Last());
                }
                
                outline.AddSegment(segment);
                for (var index = 0; index < outline.Points.Count; index++)
                {
                    var point = outline.Points[index];

                    switch (segment.Points.Count)
                    {
                        case 0:
                            segment.AddPoint(point);
                            continue;
                        case 1 when !point.IsControl:
                            segment.AddPoint(point);
                            segment = new OutlineSegment();
                            segment.Points.Add(point);
                            outline.AddSegment(segment);
                            break;
                        case 1:
                            segment.Points.Add(point);
                            break;
                        case 2 when !point.IsControl:
                            segment.AddPoint(point);
                            segment = new OutlineSegment();
                            segment.Points.Add(point);
                            outline.AddSegment(segment);
                            break;
                        case 2:
                        {
                            // 2 points in a row is control points. So, we need to calculate half point and
                            // add it as last segment point
                            var prevPoint = segment.Points[1];
                            var halfPointX = (prevPoint.X + point.X) / 2;
                            var halfPointY = (prevPoint.Y + point.Y) / 2;
                            var lastPoint = new Vector2D(halfPointX, halfPointY);
                            segment.AddPoint(lastPoint);
                            segment = new OutlineSegment();
                            segment.AddPoint(lastPoint);
                            segment.AddPoint(point);
                            outline.AddSegment(segment);
                            break;
                        }
                    }
                    
                    if (IsLastSegment())
                    {
                        segment.Points.Add(outline.Segments[0].Points[0]);
                    }

                    bool IsLastSegment()
                    {
                        return index == outline.Points.Count - 1;
                    }
                }
            }
        }
        
        private void SplitOnSegmentsCFF()
        {
            foreach (var outline in Outlines)
            {
                // we assume that the outline starting from non-control point
                if (outline.Points[0].IsControl)
                {
                    throw new OutlineException("First point of outline should not be control point");
                }
                
                var segment = new List<Vector2D>();
                
                for (var index = 0; index < outline.Points.Count; index++)
                {
                    var point = outline.Points[index];
                    segment.Add(point);

                    if (!point.IsControl && segment.Count > 1) // segment is closed
                    {
                        outline.Segments.Add(new OutlineSegment(segment));
                        segment = new List<Vector2D>();
                        segment.Add(point); // add the same non-control point as start of new segment
                    }
                }
                
                // add the first point of current outline as the last point of the last segment
                segment.Add(outline.Points[0]);
            }
        }

        internal void AddOutline(Outline outline)
        {
            outlines.Add(outline);
        }

        public SampledOutline[] TransformOutlines(Matrix3x2 matrix, byte rate)
        {
            var transformedOutlines = new List<SampledOutline>();
            if (sampledOutlinesCache.TryGetValue(rate, out var outlines))
            {
                foreach (var outline in outlines)
                {
                    var points = TransformPoints(outline.Points, matrix);
                    
                    var transformedOutline = new SampledOutline(points);
                    transformedOutlines.Add(transformedOutline);
                }
            }
            
            return transformedOutlines.ToArray();
        }

        internal void SetOutlinesForRate(byte rate, SampledOutline[] outlines)
        {
            sampledOutlinesCache[rate] = outlines;
        }

        private Vector2D[] TransformPoints(IEnumerable<Vector2D> points, Matrix3x2 matrix)
        {
            var transformedPoints = new List<Vector2D>();
            foreach (var point in points)
            {
                var transformed = Matrix3x2.TransformPoint(matrix, point);
                transformedPoints.Add(transformed);
            }

            return transformedPoints.ToArray();
        }

        internal void SetUnicodes(IEnumerable<UInt32> unicodeSet)
        {
            foreach (var unicode in unicodeSet)
            {
                if (uniqueUnicodes.Add(unicode))
                {
                    unicodes.Add(unicode);
                }
            }
        }

        internal Glyph RecalculateBounds()
        {
            if (IsEmpty) return this;
            
            var allPoints = Outlines.SelectMany(x => x.Points).Where(x => !x.IsControl).ToArray();
            var minX = (int)allPoints.Min(x => x.X);
            var minY = (int)allPoints.Min(x => x.Y);
            var maxX = (int)allPoints.Max(x => x.X);
            var maxY = (int)allPoints.Max(x => x.Y);
            BoundingRectangle = Rectangle.FromCorners(minX, minY, maxX, maxY);

            return this;
        }

        internal void SetInstructions(byte[] instructions)
        {
            Instructions = instructions;
        }
        
        public override string ToString()
        {
            return $"Name: {Name} Index: {Index}, SID: {SID} Unicodes: {string.Join(", ", Unicodes)}";
        }

        internal static Glyph Create(uint index)
        {
            return new Glyph(index);
        }

        internal Glyph SetCommands(List<Command> commands)
        {
            commandList = commands;
            return this;
        }
        
        internal Glyph FillOutlines(VariationRegionList regionList = null, float[] variationPoint = null)
        {
            var interpreter = new CommandInterpreter();
            Outline outline = null;

            foreach (var command in commandList)
            {
                if (command.IsNewOutline())
                {
                    outline = new Outline();
                    AddOutline(outline);
                }

                var pts = interpreter.GetOutlinePoints(command, regionList, variationPoint);
                outline?.Points.AddRange(pts);
            }

            return this;
        }

        private Vector3F[] RemoveSelfIntersections(in SampledOutline[] outlines)
        {
            bool isPointInside = false;
            segments = new List<LineSegment2D>();
            var localSegments = new List<LineSegment2D>();
            foreach (var outline in outlines)
            {
                for (var index = 0; index < outline.Points.Length - 1; index++)
                {
                    var start = outline.Points[index];
                    var end = outline.Points[index + 1];
                    var segment = new LineSegment2D(start, end);
                    localSegments.Add(segment);
                }
            }

            for (var i = 0; i < localSegments.Count; i++)
            {
                var checkedSegment = localSegments[i];
                var intersections = new List<DistancedPoint>();
                for (var j = i+1; j < localSegments.Count; j++)
                {
                    var currentSegment = localSegments[j];
                    if (Collision2D.SegmentSegmentIntersection(ref checkedSegment, ref currentSegment, out var point) && 
                        (point != checkedSegment.Start &&
                        point != checkedSegment.End))
                    {
                        var distance = (point - checkedSegment.Start).Length();
                        intersections.Add(new DistancedPoint(point, distance));
                    }
                }

                var sortedIntersections = intersections.OrderBy(x => x.Distance).ToArray();
                var start = checkedSegment.Start;
                for (int j = 0; j < sortedIntersections.Length; j++)
                {
                    if (!isPointInside)
                    {
                        var segment = new LineSegment2D(start, sortedIntersections[j].Point);
                        segments.Add(segment);
                    }
                    else
                    {
                        start = sortedIntersections[j].Point;
                    }
                    
                    isPointInside = !isPointInside;
                }

                if (!isPointInside)
                {
                    var lastSegment = new LineSegment2D(start, checkedSegment.End);
                    segments.Add(lastSegment);
                }
            }

            var points = new List<Vector3F>();

            foreach (var segment in segments)
            {
                points.Add(new Vector3F((float)segment.Start.X, (float)segment.Start.Y, 0));
                points.Add(new Vector3F((float)segment.End.X, (float)segment.End.Y, 0));
            }

            return points.ToArray();
        }

        // --- DIRECT MSDF START ---

        private void ColorSegments()
        {
            Color currentColor = Color.FromRgba(255, 0 ,255, 255);

            for (int i = 0; i < segments.Count; ++i)
            {
                var currentSegment = segments[i];

                if (i != (segments.Count - 1))
                {
                    var nextSegment = segments[i + 1];

                    // new outline detected, reset current color
                    if (!IsSegmentsConnected(ref currentSegment, ref nextSegment))
                    {
                        currentColor = Color.FromRgba(255, 0, 255, 255);
                    }
                }

                currentSegment.MsdfColor = currentColor;
                segments[i] = currentSegment;

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

        private double GetDistanceToSegment(LineSegment2D segment, Vector2D point)
        {
            return point.DistanceToPoint(segment.Start, segment.End);
        }

        private double GetSignedDistanceToSegment(LineSegment2D segment, Vector2D point)
        {
            var distance = point.DistanceToPoint(segment.Start, segment.End);
            
            var ray = new Ray2D(point, Vector2D.UnitX);
            var intersectionsCount = 0;
            
            foreach (var seg in segments)
            {
                var currentSeg = seg;
                if (Collision2D.RaySegmentIntersection(ref ray, ref currentSeg, out var collision))
                {
                    intersectionsCount++;
                }
            }

            // if outside of glyph - distance should be negative
            if (intersectionsCount % 2 == 0)
            {
                distance = -distance;
            }

            return distance;
        }
        
        private double GetSignedPseudoDistanceToSegment(LineSegment2D segment, Vector2D point)
        {
            var distance = MathHelper.PointToLineDistance(segment.Start, segment.End, point);
            
            var ray = new Ray2D(point, Vector2D.UnitX);
            var intersectionsCount = 0;
            
            foreach (var seg in segments)
            {
                var currentSeg = seg;
                if (Collision2D.RaySegmentIntersection(ref ray, ref currentSeg, out var collision))
                {
                    intersectionsCount++;
                }
            }

            // if outside of glyph - distance should be negative
            if (intersectionsCount % 2 == 0)
            {
                distance = -distance;
            }

            return distance;
        }
        
        private Color ApplyColorMask(Color color, bool redMask, bool greenMask, bool blueMask)
        {
            color.R *= redMask ? (byte)1 : (byte)0;
            color.G *= greenMask ? (byte)1 : (byte)0;
            color.B *= blueMask ? (byte)1 : (byte)0;

            return color;
        }
        
        private struct ColoredDistance
        {
            public double redDistance;
            public double greenDistance;
            public double blueDistance;
        }
        
        private void GetColoredDistances(Vector2D point, out double redDistance, out double greenDistance, out double blueDistance)
        {
            double closestRedDistance = Double.MaxValue;
            double closestGreenDistance = Double.MaxValue;
            double closestBlueDistance = Double.MaxValue;

            LineSegment2D closestRedSegment = new LineSegment2D();
            LineSegment2D closestGreenSegment = new LineSegment2D();
            LineSegment2D closestBlueSegment = new LineSegment2D();
            
            foreach (var segment in segments)
            {
                var distance = GetDistanceToSegment(segment, point);

                if (ApplyColorMask(segment.MsdfColor, true, false, false) != Colors.Black
                    && distance < closestRedDistance)
                {
                    closestRedDistance = distance;
                    closestRedSegment = segment;
                }
                
                if (ApplyColorMask(segment.MsdfColor, false, true, false) != Colors.Black
                    && distance < closestGreenDistance)
                {
                    closestGreenDistance = distance;
                    closestGreenSegment = segment;
                }
                
                if (ApplyColorMask(segment.MsdfColor, false, false, true) != Colors.Black
                    && distance < closestBlueDistance)
                {
                    closestBlueDistance = distance;
                    closestBlueSegment = segment;
                }
            }
            
            redDistance = GetSignedPseudoDistanceToSegment(closestRedSegment, point);
            greenDistance = GetSignedPseudoDistanceToSegment(closestGreenSegment, point);
            blueDistance = GetSignedPseudoDistanceToSegment(closestBlueSegment, point);
        }

        private double MaxOfThree(double d1, double d2, double d3)
        {
            return Math.Max(d1, Math.Max(d2, d3));
        }
        
        private double MinOfThree(double d1, double d2, double d3)
        {
            return Math.Min(d1, Math.Min(d2, d3));
        }

        private double GetNormalizedMultiplier(double originalValue, double minValue, double maxValue)
        {
            return ((originalValue - minValue) / (maxValue - minValue));
        }
        
        public Color[] GenerateDirectMSDF(uint size)
        {
            // 1. Color all segments
            ColorSegments();
            
            // 2. Calculate boundaries for original glyph
            float glyphSize = Math.Max(BoundingRectangle.Width, BoundingRectangle.Height);
            glyphSize += glyphSize * 0.3f;
            var originalDimensions = new Vector2D(glyphSize);
            
            // 3. Place glyph in center of calculated boundaries
            var glyphCenter = BoundingRectangle.Center;
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
            double maxDistance = Double.MinValue;
            //double minDistance = Double.MaxValue;

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    // determine the closest segment to current sampling point
                    var samplingPoint = new Vector2D(originalDimensions.X / size * x, originalDimensions.Y - (originalDimensions.Y / size * y));
                    
                    GetColoredDistances(samplingPoint, out coloredDistances[x, y].redDistance, out coloredDistances[x, y].greenDistance, out coloredDistances[x, y].blueDistance);

                    var maxOfThree = MaxOfThree(coloredDistances[x, y].redDistance, coloredDistances[x, y].greenDistance, coloredDistances[x, y].blueDistance);
                    var minOfThree = MinOfThree(coloredDistances[x, y].redDistance, coloredDistances[x, y].greenDistance, coloredDistances[x, y].blueDistance);

                    if (maxOfThree > maxDistance)
                    {
                        maxDistance = maxOfThree;
                    }

                    /*if (minOfThree < minDistance)
                    {
                        minDistance = minOfThree;
                    }*/
                }
            }
            
            // 5. Normalize colored pseudo-distance field to [0 .. 255] range
            var msdf = new List<Color>();

            double minDistance = -maxDistance;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (coloredDistances[x, y].redDistance < minDistance)
                    {
                        coloredDistances[x, y].redDistance = minDistance;
                    }
                    
                    if (coloredDistances[x, y].greenDistance < minDistance)
                    {
                        coloredDistances[x, y].greenDistance = minDistance;
                    }
                    
                    if (coloredDistances[x, y].blueDistance < minDistance)
                    {
                        coloredDistances[x, y].blueDistance = minDistance;
                    }
                }
            }
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    coloredDistances[x, y].redDistance = GetNormalizedMultiplier(coloredDistances[x, y].redDistance, minDistance, maxDistance) * 255;
                    coloredDistances[x, y].greenDistance = GetNormalizedMultiplier(coloredDistances[x, y].greenDistance, minDistance, maxDistance) * 255;
                    coloredDistances[x, y].blueDistance = GetNormalizedMultiplier(coloredDistances[x, y].blueDistance, minDistance, maxDistance) * 255;

                    var color = Color.FromRgba((byte)coloredDistances[x, y].redDistance, (byte)coloredDistances[x, y].greenDistance, (byte)coloredDistances[x, y].blueDistance, 255);
                    msdf.Add(color);
                }
            }

            return msdf.ToArray();
        }

        public void SetTestSegmentData()
        {
            segments = new List<LineSegment2D>();

            var s1 = new LineSegment2D(new Vector2D(0, 0), new Vector2D(300, 400));
            var s2 = new LineSegment2D(new Vector2D(300, 400), new Vector2D(500, 0));
            var s3 = new LineSegment2D(new Vector2D(500, 0), new Vector2D(0, 0));

            var s4 = new LineSegment2D(new Vector2D(100, 100), new Vector2D(300, 300));
            var s5 = new LineSegment2D(new Vector2D(300, 300), new Vector2D(400, 100));
            var s6 = new LineSegment2D(new Vector2D(400, 100), new Vector2D(100, 100));
            
            segments.Add(s1);
            segments.Add(s2);
            segments.Add(s3);
            
            segments.Add(s4);
            segments.Add(s5);
            segments.Add(s6);

            var rectangle = BoundingRectangle;

            rectangle.X = 0;
            rectangle.Y = 0;
            rectangle.Width = 500;
            rectangle.Height = 400;

            BoundingRectangle = rectangle;
        }
        
        // --- DIRECT MSDF END ---
        
        public Color[] GenerateMSDF(uint size)
        {
            float convexThreshold = 135;
            float concaveThreshold = 225;

            for (int i = 1; i <= segments.Count; ++i)
            {
                LineSegment2D prev;
                LineSegment2D current;
                
                // cycle through all segment pairs, including last-first
                if (i == segments.Count)
                {
                    prev = segments[i-1];
                    current = segments[0];
                }
                else
                {
                    prev = segments[i - 1];
                    current = segments[i];
                }
                
                // if segments are not connected - they are from different outlines, skip
                if (!IsSegmentsConnected(ref prev, ref current)) continue;

                // revert prev direction in order to get correct angle between prev and current
                var newPrev = new LineSegment2D(prev.End, prev.Start);

                // angle will be between 0 and 180
                var angle = MathHelper.DetermineAngleInDegrees(newPrev.Start, newPrev.End, current.Start, current.End);
                
                // we must determine if prev is looking outside of glyph (convex corner), or inside (concave corner)
                var prevRay = new Ray2D(prev.Start, prev.Direction);
                int prevIntersectionsCount = 0;

                foreach (var segment in segments)
                {
                    var seg = segment;
                    if (Collision2D.RaySegmentIntersection(ref prevRay, ref seg, out var collision))
                    {
                        prevIntersectionsCount++;
                    }
                }
                
                // adjust the angle depending if it is the concave case
                if (prevIntersectionsCount % 2 != 0) // concave
                {
                    angle = 360 - angle;
                }

                // check the threshold, color segments and their ends
                if (angle < convexThreshold)
                {
                    prev.EndOuterColor = current.StartOuterColor = Colors.Black;
                    if (prev.OuterColor != Colors.Black)
                    {
                        current.OuterColor = Color.Subtract(prev.InnerColor, prev.OuterColor);
                        current.InnerColor = prev.InnerColor;
                    }
                    else
                    {
                        prev.OuterColor = Colors.Red;
                        current.OuterColor = Colors.Blue;
                        prev.InnerColor = current.InnerColor = new Color(255, 0, 255);
                    }

                    prev.EndInnerColor = current.StartInnerColor = prev.InnerColor;
                }
                else if (angle > concaveThreshold)
                {
                    prev.EndInnerColor = current.StartInnerColor = Colors.White;
                    if (prev.OuterColor != Colors.Black)
                    {
                        current.OuterColor = prev.OuterColor;
                        var newColor = Color.Subtract(Colors.White, prev.InnerColor);
                        current.InnerColor = Color.Add(prev.OuterColor, newColor);
                    }
                    else
                    {
                        prev.InnerColor = new Color(255, 0, 255);
                        current.InnerColor = new Color(0, 255, 255);;
                        prev.OuterColor = current.OuterColor = Colors.Blue;
                    }
                    prev.EndOuterColor = current.StartOuterColor = prev.OuterColor;
                }
                
                if (i == segments.Count)
                {
                    segments[i - 1] = prev;
                    segments[0] = current;
                }
                else
                {
                    segments[i - 1] = prev;
                    segments[i] = current;
                }
            }
            
            // calculate boundaries for original glyph
            float glyphSize = Math.Max(BoundingRectangle.Width, BoundingRectangle.Height);
            glyphSize += glyphSize * 0.1f;
            var originalDimensions = new Vector2D(glyphSize);
            
            // calculate diff for placing glyph in center of calculated boundaries
            var glyphCenter = new Vector2D(BoundingRectangle.Width / 2.0f, BoundingRectangle.Height / 2.0f);
            glyphCenter.X += BoundingRectangle.X;
            glyphCenter.Y += BoundingRectangle.Y;
            var originalCenter = originalDimensions / 2;
            var diff = originalCenter - glyphCenter;
            
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                
                var innerColor = segment.InnerColor;
                var outerColor = segment.OuterColor;
                var start = segment.Start;
                var end = segment.End;
                var startInnerColor = segment.StartInnerColor;
                var endInnerColor = segment.EndInnerColor;
                var startOuterColor = segment.StartOuterColor;
                var endOuterColor = segment.EndOuterColor;
                
                // place glyph in center of calculated boundaries
                segment = new LineSegment2D(start + diff, end + diff);
                segment.InnerColor = innerColor;
                segment.OuterColor = outerColor;
                segment.StartInnerColor = startInnerColor;
                segment.StartOuterColor = startOuterColor;
                segment.EndInnerColor = endInnerColor;
                segment.EndOuterColor = endOuterColor;
                
                segments[index] = segment;
            }

            // msdf color map
            Color[,] msdf = new Color[size, size];
            
            // sdf distances
            double[,] distances = new double[size, size];
            var distances2 = new List<double>();

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    // determine the closest segment to current sampling point
                    var point = new Vector2D(originalDimensions.X / size * x, originalDimensions.Y - (originalDimensions.Y / size * y));
                    var ray = new Ray2D(point, Vector2D.UnitX);
                    double distance = Double.MaxValue;
                    LineSegment2D closestSegment = new LineSegment2D();
                    int intersectionsCount = 0;
                    byte pointRelativePosition = 4;
                    foreach (var segment in segments)
                    {
                        var currentDistance = point.DistanceToPoint(segment.Start, segment.End, out var tmpPointRelativePosition);
                        if (currentDistance <= distance)
                        {
                            distance = currentDistance;
                            distances[x, y] = distance;
                            closestSegment = segment;
                            pointRelativePosition = tmpPointRelativePosition;
                        }
                        var seg = segment;
                        if (Collision2D.RaySegmentIntersection(ref ray, ref seg, out var collision))
                        {
                            intersectionsCount++;
                        }
                    }

                    if (intersectionsCount % 2 == 0)
                    {
                        distances[x, y] = -distances[x, y];
                    }
                    distances2.Add(distances[x, y]);

                    // apply coloring to point with respect quad position (inside / outside, away / on top of segment)
                    if (intersectionsCount % 2 == 0)
                    {
                        if (pointRelativePosition == 0)
                        {
                            msdf[x, y] = closestSegment.StartOuterColor;
                        }
                        else if(pointRelativePosition == 1)
                        {
                            msdf[x, y] = closestSegment.OuterColor;
                        }
                        else if (pointRelativePosition == 2)
                        {
                            msdf[x, y] = closestSegment.EndOuterColor;
                        }
                    }
                    else
                    {
                        if (pointRelativePosition == 0)
                        {
                            msdf[x, y] = closestSegment.StartInnerColor;
                        }
                        else if(pointRelativePosition == 1)
                        {
                            msdf[x, y] = closestSegment.InnerColor;
                        }
                        else if (pointRelativePosition == 2)
                        {
                            msdf[x, y] = closestSegment.EndInnerColor;
                        }
                    }
                    
                    // workaround for correct saving in .png
                    msdf[x, y].A = 250;
                }
            }

            /*var max = distances2.Max();
            //var min = distances2.Min();
            var min = -max;
            
            var value = max - min;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (distances[x, y] < min)
                    {
                        distances[x, y] = min;
                    }
                }
            }
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var sdfValue = distances[x, y];
                    var b = (sdfValue - min) / value;
                    distances[x, y] = b;
                }
            }*/

            List<Color> colors = new List<Color>();
            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    var color = msdf[x, y];
                    /*var dist = distances[x, y];
                    color.R = (byte)(color.R * dist);
                    color.G = (byte)(color.G * dist);
                    color.B = (byte)(color.B * dist);*/
                    colors.Add(color);
                }
            }

            return colors.ToArray();
        }

        private bool IsSegmentsConnected(ref LineSegment2D segment1, ref LineSegment2D segment2)
        {
            return segment1.End == segment2.Start;
        }

        public Color[] GenerateSDF(uint size)
        {
            var doubleArray = new Double[size, size];
            var commonList = new List<double>();

            float glyphSize = Math.Max(BoundingRectangle.Width, BoundingRectangle.Height);
            glyphSize += glyphSize * 0.1f;
            var originalDimensions = new Vector2D(glyphSize);
            var glyphCenter = BoundingRectangle.Center;
            var originalCenter = originalDimensions / 2;
            var diff = originalCenter - glyphCenter;
            
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                var start = segment.Start;
                var end = segment.End;
                segment = new LineSegment2D(start + diff, end + diff);
                segments[index] = segment;
            }

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    var point = new Vector2D(originalDimensions.X / size * x, originalDimensions.Y - (originalDimensions.Y / size * y));
                    var ray = new Ray2D(point, Vector2D.UnitX);
                    var distances = new List<Double>();
                    int intersectionsCount = 0;
                    foreach (var segment in segments)
                    {
                        distances.Add(point.DistanceToPoint(segment.Start, segment.End));
                        var seg = segment;
                        if (Collision2D.RaySegmentIntersection(ref ray, ref seg, out var collision))
                        {
                            intersectionsCount++;
                        }
                    }

                    var minValue = distances.Min();

                    if (intersectionsCount % 2 == 0)
                    {
                        minValue = -minValue;
                    }

                    doubleArray[x, y] = minValue;
                    commonList.Add(minValue);
                }
            }

            //var min = commonList.Min();
            var max = commonList.Max();
            var min = -max;
            var value = max - min;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (doubleArray[x, y] < min)
                    {
                        doubleArray[x, y] = min;
                    }
                }
            }
            
            var bytes = new List<Color>();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var sdfValue = doubleArray[x, y];
                    var b = (byte)((sdfValue - min) / value * 255);
                    Debug.WriteLine($"SDF value = {sdfValue}");
                    bytes.Add(new Color(b, (byte)0, (byte)0, (byte)255));
                }
            }

            return bytes.ToArray();
        }
        
        public Color[] GenerateSPDF(uint size)
        {
            var doubleArray = new Double[size, size];
            var commonList = new List<double>();

            float glyphSize = Math.Max(BoundingRectangle.Width, BoundingRectangle.Height);
            glyphSize += glyphSize * 0.1f;
            var originalDimensions = new Vector2D(glyphSize);
            var glyphCenter = BoundingRectangle.Center;
            var originalCenter = originalDimensions / 2;
            var diff = originalCenter - glyphCenter;
            
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                var start = segment.Start;
                var end = segment.End;
                segment = new LineSegment2D(start + diff, end + diff);
                segments[index] = segment;
            }

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    var point = new Vector2D(originalDimensions.X / size * x, originalDimensions.Y - (originalDimensions.Y / size * y));
                    var ray = new Ray2D(point, Vector2D.UnitX);
                    var closestDist = Double.MaxValue;
                    var closestSeg = new LineSegment2D();
                    int intersectionsCount = 0;
                    foreach (var segment in segments)
                    {
                        var dist = point.DistanceToPoint(segment.Start, segment.End);

                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestSeg = segment;
                        }

                        var seg = segment;
                        if (Collision2D.RaySegmentIntersection(ref ray, ref seg, out var collision))
                        {
                            intersectionsCount++;
                        }
                    }

                    var pseudoDist = MathHelper.PointToLineDistance(closestSeg.Start, closestSeg.End, point);

                    if (intersectionsCount % 2 == 0)
                    {
                        pseudoDist = -pseudoDist;
                    }

                    doubleArray[x, y] = pseudoDist;
                    commonList.Add(pseudoDist);
                }
            }

            //var min = commonList.Min();
            var max = commonList.Max();
            var min = -max;
            var value = max - min;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (doubleArray[x, y] < min)
                    {
                        doubleArray[x, y] = min;
                    }
                }
            }
            
            var bytes = new List<Color>();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var sdfValue = doubleArray[x, y];
                    var b = (byte)((sdfValue - min) / value * 255);
                    Debug.WriteLine($"SDF value = {sdfValue}");
                    bytes.Add(new Color(b, (byte)0, (byte)0, (byte)255));
                }
            }

            return bytes.ToArray();
        }
        
        private struct DistancedPoint
        {
            public DistancedPoint(Vector2D point, double distance)
            {
                Point = point;
                Distance = distance;
            }
            
            public Vector2D Point;

            public Double Distance;
        }
    }
}