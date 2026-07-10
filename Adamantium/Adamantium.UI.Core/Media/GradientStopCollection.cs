using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Adamantium.UI.Core.Media;

/// <summary>The ordered set of <see cref="GradientStop"/>s of a <see cref="GradientBrush"/>. Observable so a bound stop
/// edit invalidates the brush; the render side reads a snapshot sorted by offset.</summary>
public sealed class GradientStopCollection : ObservableCollection<GradientStop>
{
    public GradientStopCollection() { }

    public GradientStopCollection(IEnumerable<GradientStop> stops) : base(stops) { }
}
