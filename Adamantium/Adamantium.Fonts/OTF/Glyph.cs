using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.OTF
{
    public class Glyph
    {
        internal OutlineList OutlineList { get; }
        private Dictionary<uint, SampledOutline[]> sampledOutlinesCache;
        private Dictionary<uint, Vector3F[]> triangulatedCache;

        public uint Index { get; }
        public uint Unicode { get; internal set; }

        public Glyph(uint index)
        {
            Index = index;
            OutlineList = new OutlineList();
            sampledOutlinesCache = new Dictionary<uint, SampledOutline[]>();
            triangulatedCache = new Dictionary<uint, Vector3F[]>();
        }
        
        public SampledOutline[] Sample(uint rate)
        {
            var sampledOutlines = BezierSampler.SampleOutlines(OutlineList.Outlines.ToArray(), rate);
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
                
                 // var lst = new List<Vector3F>();
                 // foreach (var outline in sampledOutlines)
                 // {
                 //     for (int i = 0; i < outline.Points.Length; i++)
                 //     {
                 //         lst.Add((Vector3F)outline.Points[i]);
                 //     }
                 // }
                
                //return lst.ToArray();
            }

            return points;
        }
    }
}