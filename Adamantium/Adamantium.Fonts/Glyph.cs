using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.OTF;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public class Glyph
    {
        internal OutlineList Outlines { get; }
        private readonly Dictionary<uint, SampledOutline[]> sampledOutlinesCache;
        private readonly Dictionary<uint, Vector3F[]> triangulatedCache;

        public uint Index { get; }
        public uint Unicode => Unicodes.FirstOrDefault();
        
        public List<UInt32> Unicodes { get; }
        public string Name { get; internal set; }
        internal UInt32 SID { get; set; }

        public Glyph(uint index)
        {
            Index = index;
            Outlines = new OutlineList();
            sampledOutlinesCache = new Dictionary<uint, SampledOutline[]>();
            triangulatedCache = new Dictionary<uint, Vector3F[]>();
            Unicodes = new List<uint>();
        }
        
        public SampledOutline[] Sample(uint rate)
        {
            var sampledOutlines = BezierSampler.SampleOutlines(Outlines.Outlines.ToArray(), rate);
            sampledOutlinesCache[rate] = sampledOutlines;
            return sampledOutlines;
        }

        public Vector3F[] Triangulate(uint rate)
        {
            if (!triangulatedCache.TryGetValue(rate, out var points))
            {
                if (!sampledOutlinesCache.TryGetValue(rate, out var sampledOutlines))
                {
                    sampledOutlines = Sample(rate);
                }
                
                var polygon = new Polygon();
                foreach (var outline in sampledOutlines)
                {
                    polygon.Polygons.Add(new PolygonItem(outline.Points));
                }
                
                points = polygon.Fill().ToArray();
                triangulatedCache[rate] = points;
            }

            return points;
        }

        public override string ToString()
        {
            return $"Name: {Name} Index: {Index}, SID: {SID} Unicodes: {string.Join(", ", Unicodes)}";
        }
    }
}