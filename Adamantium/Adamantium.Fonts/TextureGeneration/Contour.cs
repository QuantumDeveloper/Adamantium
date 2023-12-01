using System.Collections.Generic;

namespace Adamantium.Fonts.TextureGeneration;

internal class Contour
{
    public List<Edge> Edges;

    public Contour()
    {
        Edges = new List<Edge>();
    }
}