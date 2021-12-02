﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using Adamantium.Engine.Compiler.Converter.AutoGenerated;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Exceptions;
using Adamantium.Fonts.Parsers.CFF;
using Adamantium.Fonts.Tables.CFF;
using Adamantium.Mathematics;
using Adamantium.UI;
using Matrix3x2 = Adamantium.Mathematics.Matrix3x2;
using Vector2 = Adamantium.Mathematics.Vector2;

namespace Adamantium.Fonts
{
    public class Glyph
    {
        private readonly object lockObject = new object();
        
        private HashSet<UInt32> uniqueUnicodes;
        private List<UInt32> unicodes;

        private List<Outline> outlines;
        private bool isSplitOnSegments;
        private List<LineSegment2D> mergedOutlinesSegments;
        internal IReadOnlyCollection<Outline> Outlines => outlines.AsReadOnly();
        private readonly Dictionary<uint, SampledOutline[]> sampledOutlinesCache;
        
        private List<Command> commandList;

        private MsdfGenerator msdfGenerator;
        private SubpixelRasterizer subpixelRasterizer;

        public double   EmRelatedLeftSideBearingMultiplier { get; private set; }
        public double   EmRelatedAdvanceWidthMultiplier { get; private set; }
        public Vector2 EmRelatedCenterToBaseLineMultiplier { get; private set; }

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
            return new Glyph(index, true, OutlineType.Unknown);
        }

        public Glyph(uint index, OutlineType outlineType)
        {
            Index = index;
            Name = String.Empty;
            outlines = new List<Outline>();
            sampledOutlinesCache = new Dictionary<uint, SampledOutline[]>();
            unicodes = new List<uint>();
            uniqueUnicodes = new HashSet<uint>();
            CompositeGlyphComponents = new List<CompositeGlyphComponent>();
            msdfGenerator = new MsdfGenerator();
            subpixelRasterizer = new SubpixelRasterizer();
            OutlineType = outlineType;
        }

        private Glyph(uint index, bool isEmpty, OutlineType outlineType) : this(index, outlineType)
        {
            IsEmpty = isEmpty;
        }

        public Vector2[] GetTextureAtlasUVCoordinates(uint glyphTextureSize, uint atlasStartGlyphIndex, uint glyphCount)
        {
            var uv = new Vector2[2];

            var glyphsPerRow = (uint) Math.Ceiling(Math.Sqrt(glyphCount));
            var glyphsPerColumn = (uint) Math.Ceiling((double) glyphCount / glyphsPerRow);

            var atlasDimensions = new Size(glyphsPerRow, glyphsPerColumn);

            var relativeGlyphIndex = Index - atlasStartGlyphIndex;

            var startX = (relativeGlyphIndex % (int) atlasDimensions.Width) * glyphTextureSize;
            var startY = (relativeGlyphIndex / (int) atlasDimensions.Width) * glyphTextureSize;

            Size atlasSize = new Size(atlasDimensions.Width * glyphTextureSize,
                atlasDimensions.Height * glyphTextureSize);

            var uvStart = new Vector2(startX / atlasSize.Width, startY / atlasSize.Height);
            var uvEnd = new Vector2((startX + glyphTextureSize) / atlasSize.Width,
                (startY + glyphTextureSize) / atlasSize.Height);

            uv[0] = uvStart;
            uv[1] = uvEnd;

            return uv;
        }

        public void CalculateEmRelatedMultipliers(ushort unitsPerEm)
        {
            CalculateEmRelatedLeftSideBearingMultiplier(unitsPerEm);
            CalculateEmRelatedCenterToBaseLineMultiplier(unitsPerEm);
            CalculateEmRelatedAdvanceWidthMultiplier(unitsPerEm);
        }

        public Vector3F[] Sample(byte rate)
        {
            if (IsEmpty)
            {
                return Array.Empty<Vector3F>();
            }
            
            if (rate == 0) rate = 1;

            try
            {
                Monitor.TryEnter(lockObject);
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

                //AutoHint();

                return points;
            }
            finally
            {
                Monitor.Exit(lockObject);
            }
        }

        private void CalculateEmRelatedLeftSideBearingMultiplier(ushort unitsPerEm)
        {
            EmRelatedLeftSideBearingMultiplier = (double)LeftSideBearing / unitsPerEm;
        }

        private void CalculateEmRelatedAdvanceWidthMultiplier(ushort unitsPerEm)
        {
            EmRelatedAdvanceWidthMultiplier = (double)AdvanceWidth / unitsPerEm;
        }
        
        private void CalculateEmRelatedCenterToBaseLineMultiplier(ushort unitsPerEm)
        {
            var emSquare = new Rectangle(0, 0, unitsPerEm, unitsPerEm);
            var diff = emSquare.Center - BoundingRectangle.Center;

            EmRelatedCenterToBaseLineMultiplier = new Vector2(diff.X / unitsPerEm, diff.Y / unitsPerEm);
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
                            var lastPoint = new Vector2(halfPointX, halfPointY);
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
                
                var segment = new List<Vector2>();
                
                for (var index = 0; index < outline.Points.Count; index++)
                {
                    var point = outline.Points[index];
                    segment.Add(point);

                    if (!point.IsControl && segment.Count > 1) // segment is closed
                    {
                        outline.Segments.Add(new OutlineSegment(segment));
                        segment = new List<Vector2>();
                        segment.Add(point); // add the same non-control point as start of new segment
                    }
                }
                
                // currently segment must contain exactly one point
                if (segment.Count != 1)
                {
                    throw new Exception($"Segment must contain 1 point currently, actual points count = {segment.Count}");
                }

                // add the first point of current outline as the last point of the last segment
                // but only if these two points are not equal
                // in some cases outline assumes we build this list segment (like in 'A')
                // but in some cases outline's last and first points are equal (like in 'O'), so we do not need to build this last segment
                if (segment[0] != outline.Points[0])
                {
                    segment.Add(outline.Points[0]);
                    outline.Segments.Add(new OutlineSegment(segment));
                }
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

        private Vector2[] TransformPoints(IEnumerable<Vector2> points, Matrix3x2 matrix)
        {
            var transformedPoints = new List<Vector2>();
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

        internal static Glyph Create(uint index, OutlineType outlineType)
        {
            return new Glyph(index, outlineType);
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

        private struct DistancedPoint
        {
            public DistancedPoint(Vector2 point, double distance)
            {
                Point = point;
                Distance = distance;
            }
            
            public Vector2 Point;

            public Double Distance;
        }
        
        private Vector3F[] RemoveSelfIntersections(in SampledOutline[] outlines)
        {
            bool isPointInside = false;
            mergedOutlinesSegments = new List<LineSegment2D>();
            var localSegments = new List<LineSegment2D>();
            var intersections = new List<DistancedPoint>();

            foreach (var outline in outlines)
            {
                foreach (var segment in outline.Segments)
                {
                    localSegments.Add(segment);
                }
            }
            
            for (var i = 0; i < localSegments.Count; i++)
            {
                var checkedSegment = localSegments[i];
                intersections.Clear();
                for (var j = i+1; j < localSegments.Count; j++)
                {
                    // find all intersections of checked segment with the rest of segments
                    var currentSegment = localSegments[j];
                    if (Collision2D.SegmentSegmentIntersection(ref checkedSegment, ref currentSegment, out var point) && 
                        (point != checkedSegment.Start &&
                        point != checkedSegment.End))
                    {
                        var distance = (point - checkedSegment.Start).Length();
                        intersections.Add(new DistancedPoint(point, distance));
                    }
                }

                // sort intersections by distance from checked segment start - we will then be switching "inside" flag on each intersection
                var sortedIntersections = intersections.OrderBy(x => x.Distance).ToArray();
                var start = checkedSegment.Start;
                for (int j = 0; j < sortedIntersections.Length; j++)
                {
                    if (!isPointInside)
                    {
                        var segment = new LineSegment2D(start, sortedIntersections[j].Point);
                        mergedOutlinesSegments.Add(segment);
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
                    mergedOutlinesSegments.Add(lastSegment);
                }
            }

            var points = new List<Vector3F>();

            foreach (var segment in mergedOutlinesSegments)
            {
                points.Add(new Vector3F((float)segment.Start.X, (float)segment.Start.Y, 0));
                points.Add(new Vector3F((float)segment.End.X, (float)segment.End.Y, 0));
            }

            return points.ToArray();
        }

        private void AutoHint()
        {
            var indicesToHint = new List<int>();

            for (var i = 0; i < mergedOutlinesSegments.Count; i++)
            {
                if (mergedOutlinesSegments[i].Start.X == mergedOutlinesSegments[i].End.X ||
                    mergedOutlinesSegments[i].Start.Y == mergedOutlinesSegments[i].End.Y)
                {
                    indicesToHint.Add(i);
                }
            }
            
            foreach (var index in indicesToHint)
            {
                var prevIndex = index > 0 ? index - 1 : mergedOutlinesSegments.Count - 1;
                var currentIndex = index;
                var nextIndex = index < (mergedOutlinesSegments.Count - 1) ? index + 1 : 0;

                var prevSegment = mergedOutlinesSegments[prevIndex];
                var currentSegment = mergedOutlinesSegments[currentIndex];
                var nextSegment = mergedOutlinesSegments[nextIndex];

                var hintedCurrentStart = new Vector2();
                var hintedCurrentEnd = new Vector2();
                var hintedValue = 0.0;
                
                // vertical stem
                if (currentSegment.Start.X == currentSegment.End.X)
                {
                    hintedValue = Math.Round(currentSegment.Start.X);
                    hintedCurrentStart = new Vector2(hintedValue, currentSegment.Start.Y);
                    hintedCurrentEnd = new Vector2(hintedValue, currentSegment.End.Y);
                }
                
                // horizontal stem
                if (currentSegment.Start.Y == currentSegment.End.Y)
                {
                    hintedValue = Math.Round(currentSegment.Start.Y);
                    hintedCurrentStart = new Vector2(currentSegment.Start.X, hintedValue);
                    hintedCurrentEnd = new Vector2(currentSegment.End.X, hintedValue);
                }
                
                var hintedCurrentSegment = new LineSegment2D(hintedCurrentStart, hintedCurrentEnd);
                mergedOutlinesSegments[currentIndex] = hintedCurrentSegment;

                if (GlyphSegmentsMath.IsSegmentsConnected(ref prevSegment, ref currentSegment))
                {
                    var hintedPrevSegment = new LineSegment2D(prevSegment.Start, hintedCurrentStart);
                    mergedOutlinesSegments[prevIndex] = hintedPrevSegment;
                }
                    
                if (GlyphSegmentsMath.IsSegmentsConnected(ref currentSegment, ref nextSegment))
                {
                    var hintedNextSegment = new LineSegment2D(hintedCurrentEnd, nextSegment.End);
                    mergedOutlinesSegments[nextIndex] = hintedNextSegment;
                }
            }
        }
        
        public Color[,] GenerateDirectMSDF(uint size, double pxRange, ushort em)
        {
            try
            {
                Monitor.TryEnter(lockObject);
                return msdfGenerator.GenerateDirectMSDF(size, pxRange, BoundingRectangle, mergedOutlinesSegments, em);
            }
            finally
            {
                Monitor.Exit(lockObject);
            }
        }

        public Color[,] RasterizeGlyphBySubpixels(uint textSize, ushort em)
        {
            try
            {
                Monitor.TryEnter(lockObject);
                return subpixelRasterizer.RasterizeGlyphBySubpixels(
                    textSize, 
                    BoundingRectangle, 
                    mergedOutlinesSegments,
                    em);
            }
            finally
            {
                Monitor.Exit(lockObject);
            }
        }
        
        // for visualizing
        public byte[,] GetVisSubpixels()
        {
            return subpixelRasterizer.GetVisSubpixels();
        }

        public void GetSegments(out List<Vector3F> vertexList, out List<Color> colorList)
        {
            vertexList = new List<Vector3F>();
            colorList = new List<Color>();
            
            foreach (var mergedSegment in mergedOutlinesSegments)
            {
                var newStart = new Vector3F((float)mergedSegment.Start.X, (float)mergedSegment.Start.Y, 0);
                var newEnd = new Vector3F((float)mergedSegment.End.X, (float)mergedSegment.End.Y, 0);
                
                vertexList.Add(newStart);
                vertexList.Add(newEnd);
                
                colorList.Add(Colors.Red);
                colorList.Add(Colors.Red);
            }
        }
    }
}