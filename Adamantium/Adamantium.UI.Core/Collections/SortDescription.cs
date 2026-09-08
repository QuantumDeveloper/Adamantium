using System.Collections.ObjectModel;

namespace Adamantium.UI.Core.Collections;

/// <summary>Which way a <see cref="SortDescription"/> orders.</summary>
public enum SortDirection
{
    /// <summary>Smallest first.</summary>
    Ascending,

    /// <summary>Largest first.</summary>
    Descending
}

/// <summary>
/// One level of ordering: a property to compare by, and which way round. Several compose in the order they are listed -
/// the second only decides ties in the first, as in any other sort.
/// <para>Declared by NAME rather than by a comparer on purpose: a name is something the view can also LISTEN to, so
/// live sorting knows which property changes matter without being told twice. That is the trade for reflection.</para>
/// </summary>
public readonly struct SortDescription(string propertyName, SortDirection direction = SortDirection.Ascending)
{
    /// <summary>The property compared, read off each item by name.</summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>Which way it orders.</summary>
    public SortDirection Direction { get; } = direction;
}

/// <summary>The ordering levels of a <see cref="CollectionView"/>, in priority order. Observable: the view re-sorts when
/// this changes.</summary>
public sealed class SortDescriptionCollection : ObservableCollection<SortDescription>;
