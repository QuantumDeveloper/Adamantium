using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI;

public sealed class MeasurableComponentsCollection : TrackingCollection<IMeasurableComponent>
{
    public MeasurableComponentsCollection()
    {
    }

    public MeasurableComponentsCollection(IEnumerable<IMeasurableComponent> elements) : base(elements)
    {
    }
}