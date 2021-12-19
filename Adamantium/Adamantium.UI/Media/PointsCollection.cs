using System.Collections.Generic;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media;

public class PointsCollection : TrackingCollection<Vector2>
{
    public PointsCollection() : base()
    {
        
    }

    public PointsCollection(IEnumerable<Vector2> points) : base(points)
    {
        
    }
    
}