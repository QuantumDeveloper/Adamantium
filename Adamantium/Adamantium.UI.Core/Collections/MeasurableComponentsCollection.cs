using Adamantium.Core.Collections;

namespace Adamantium.UI.Core.Collections;

public sealed class MeasurableComponentsCollection : TrackingCollection<IMeasurableComponent>
{
    public MeasurableComponentsCollection()
    {
    }

    public MeasurableComponentsCollection(IEnumerable<IMeasurableComponent> elements) : base(elements)
    {
    }
}