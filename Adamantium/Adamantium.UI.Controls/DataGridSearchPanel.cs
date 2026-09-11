using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>The strip above the table that searches it: a field, how many cells hold what was typed, and the two steps
/// through them.
/// <para>The search RUNS on Enter and on the button, not on every keystroke: one pass reads every shown column of
/// every row, which is not something to spend per letter - measured at some 380 ms over ten thousand rows.</para></summary>
public class DataGridSearchPanel : Control
{
    private TreeDataGrid _owner;
    private TextBox _text;
    private ButtonBase _find;
    private ButtonBase _next;
    private ButtonBase _previous;
    private ButtonBase _close;
    private ContentControl _count;

    /// <summary>The grid this strip searches. Setting it REGISTERS the strip with that grid, which is what lets a new
    /// count reach it.</summary>
    public TreeDataGrid Owner
    {
        get => _owner;
        internal set
        {
            if (ReferenceEquals(_owner, value)) return;
            _owner = value;
            _owner?.AdoptSearchPanel(this);
            Sync();
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _text = GetTemplateChild("PART_Text") as TextBox;
        _find = GetTemplateChild("PART_Find") as ButtonBase;
        _next = GetTemplateChild("PART_Next") as ButtonBase;
        _previous = GetTemplateChild("PART_Previous") as ButtonBase;
        _close = GetTemplateChild("PART_Close") as ButtonBase;
        _count = GetTemplateChild("PART_Count") as ContentControl;

        // ENTER PRESSED, not KeyDown: a single-line TextBox handles its own keys and raises this instead, so a KeyDown
        // handler here saw everything EXCEPT the one key the strip cares about.
        if (_text != null) _text.EnterPressed += OnEnterPressed;
        if (_find != null) _find.Click += OnFind;
        if (_next != null) _next.Click += OnNext;
        if (_previous != null) _previous.Click += OnPrevious;
        if (_close != null) _close.Click += OnClose;

        Sync();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        if (_text != null) _text.EnterPressed -= OnEnterPressed;
        if (_find != null) _find.Click -= OnFind;
        if (_next != null) _next.Click -= OnNext;
        if (_previous != null) _previous.Click -= OnPrevious;
        if (_close != null) _close.Click -= OnClose;

        _text = null;
        _find = null;
        _next = null;
        _previous = null;
        _close = null;
        _count = null;
    }

    /// <summary>Brings the count in line with what the grid found.</summary>
    internal void Sync()
    {
        if (_count == null) return;

        var total = Owner?.MatchCount ?? 0;
        var current = Owner?.CurrentMatch ?? 0;

        // Nothing typed says nothing: an empty field with "0 of 0" under it reads as a failed search rather than as
        // a search that was never made.
        // A walk in progress says so: the count grows as the table is read, and a number that keeps changing with no
        // word beside it reads as a table that cannot make up its mind.
        var walking = Owner?.IsSearching == true ? "…" : string.Empty;

        _count.Content = string.IsNullOrEmpty(Owner?.SearchText) ? string.Empty
            : total == 0 ? (walking.Length > 0 ? "searching…" : "no matches")
            : $"{current} of {total}{walking}";
    }

    // Enter SEARCHES, and searches again from where it stands - the same key that starts a find is the one that walks
    // it, which is what every editor does.
    private void OnEnterPressed(object sender, KeyEventArgs e)
    {
        if (Owner == null) return;

        e.Handled = true;
        if (string.Equals(Owner.SearchText, _text?.Text)) Owner.FindNext();
        else Find();
    }

    private void OnFind(object sender, RoutedEventArgs e) => Find();

    private void OnNext(object sender, RoutedEventArgs e) => Owner?.FindNext();

    private void OnPrevious(object sender, RoutedEventArgs e) => Owner?.FindPrevious();

    // Closing the strip from INSIDE it: the switch that opened it may be anywhere - a page's own settings, a menu -
    // and a strip that can only be dismissed from wherever it was summoned is a strip that stays. Taking it away calls
    // the search off with it, so nothing is left painted over the table.
    // SetCurrentValue, never the plain setter: a CLR setter writes the Local slot, which outranks Binding FOREVER, so
    // the strip would close once and the switch that opened it could never open it again - its pushes would land in a
    // slot the local write permanently masks. This writes at the slot the binding itself uses, and the target change
    // still carries back to the source.
    private void OnClose(object sender, RoutedEventArgs e)
    {
        Owner?.SetCurrentValue(TreeDataGrid.ShowSearchPanelProperty, false);
    }

    private void Find()
    {
        if (Owner == null) return;

        Owner.SetCurrentValue(TreeDataGrid.SearchTextProperty, _text?.Text);
        Owner.Search();
    }
}
