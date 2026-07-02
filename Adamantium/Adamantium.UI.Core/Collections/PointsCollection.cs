using Adamantium.Core.Collections;
using Adamantium.Core.TypeParsing;
using Adamantium.Mathematics;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Collections;

[TypeParser(typeof(PointsCollectionParser))]
public class PointsCollection : TrackingCollection<Vector2>
{
    public PointsCollection() : base()
    {
        
    }

    public PointsCollection(IEnumerable<Vector2> points) : base(points)
    {
        
    }
    
}