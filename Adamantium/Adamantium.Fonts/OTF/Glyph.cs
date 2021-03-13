using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.OTF
{
    public class Glyph
    {
        internal OutlineList OutlineList { get; }
        private Dictionary<uint, SampledOutline[]> sampledOutlinesCache;

        public uint Index { get; }
        public uint Unicode { get; internal set; }

        public Glyph(uint index)
        {
            Index = index;
            OutlineList = new OutlineList();
            sampledOutlinesCache = new Dictionary<uint, SampledOutline[]>();
        }
        
        public void Sample(uint rate)
        {
            sampledOutlinesCache[rate] = BezierSampler.SampleOutlines(OutlineList.Outlines.ToArray(), rate);
        }
    }
}