namespace Adamantium.UI.Controls;

/// <summary>The panel a row opens UNDER itself - a record's long form, shown with a template of the page's own making.
/// <para>It is a NODE of the same tree the rows live in, exactly as a group header is: the flattener splices it in after
/// the row that owns it, the virtualizer realizes it as it realizes any row, and a table of ten thousand records with
/// three of them opened still builds only what is on screen. Anything else - a panel parented to the row, a second
/// overlaid list - would have to be positioned, scrolled and recycled by hand, in parallel with the machinery that
/// already does all three.</para>
/// <para>It is spliced INDEPENDENTLY of the row's tree expansion: a row can show its details with its branch shut, and
/// open its branch with its details shut. They are two different questions and each has its own answer.</para></summary>
public sealed class DataGridRowDetails
{
    internal DataGridRowDetails(object item)
    {
        Item = item;
    }

    /// <summary>The record this panel is the long form of - what its template binds against.</summary>
    public object Item { get; }
}
