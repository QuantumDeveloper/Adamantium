using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Adamantium.UI.Core.Media.Drawings;

/// <summary>The children of a <see cref="DrawingGroup"/>, painted in order.</summary>
public sealed class DrawingCollection : ObservableCollection<Drawing>
{
    public DrawingCollection() { }

    public DrawingCollection(IEnumerable<Drawing> drawings) : base(drawings) { }
}
