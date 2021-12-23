using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models;

public class MeshLayer
{
    public Vector3F[] Points { get; set; }
    
    public bool IsGeometryClosed { get; set; }

    public MeshLayer()
    {
        
    }
    
    public MeshLayer(Vector3F[] points, bool isGeometryClosed)
    {
        Points = points;
        IsGeometryClosed = isGeometryClosed;
    }
}