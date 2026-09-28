using System.Collections.Generic;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.TextureGeneration;

internal class Edge
{
    public List<MsdfGlyphSegment> Segments;

    public Edge()
    {
        Segments = new List<MsdfGlyphSegment>();
    }
}