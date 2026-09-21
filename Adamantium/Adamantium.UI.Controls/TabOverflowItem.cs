using System;
using Adamantium.Core.Commands;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// One row of a tab strip's overflow flyout - a VIEW of a tab, never the tab.
/// <para>A <see cref="TabItem"/> is a live control and belongs to exactly one parent, so a list handed the tab itself
/// hosts it as a row's content and thereby takes it out of the strip: opening the flyout emptied the whole strip. A row
/// therefore carries what a tab SAYS - its icon, its header, whether it may be closed - each as data plus the template
/// that draws it, so the strip and the flyout can show the same tab at the same time, each building its own visual.</para>
/// <para>The look is entirely the theme's (<c>ListBox.TabOverflowList</c>'s ItemTemplate): icons, text and a close
/// button are laid out there, bound to the properties here.</para>
/// </summary>
public sealed class TabOverflowItem
{
    private readonly TabControl _owner;
    private readonly TabItem _tab;
    private readonly object _item;   // what the tab hosts - the only handle a row has on a tab with no container

    internal TabOverflowItem(TabControl owner, TabItem tab, object item, object header, DataTemplate headerTemplate)
    {
        _owner = owner;
        _tab = tab;
        _item = item;
        Header = header;
        HeaderTemplate = headerTemplate;
        Icon = tab?.Icon;
        IconTemplate = tab?.IconTemplate ?? owner?.IconTemplate;
        // Falls back to the CONTROL when there is no container: most rows here stand for tabs that are off screen, and
        // asking the missing container left every one of them without a close button.
        CanClose = tab != null ? tab.ShowCloseButton : owner is { ShowCloseButton: true };
        Close = new CloseTabCommand(this);
    }

    /// <summary>What names the tab: its header for an authored tab, the bound item itself for a data-bound one.</summary>
    public object Header { get; }

    /// <summary>How <see cref="Header"/> is drawn - the same template the strip uses, so a data-bound tab reads the same
    /// in both places.</summary>
    public DataTemplate HeaderTemplate { get; }

    public object Icon { get; }

    public DataTemplate IconTemplate { get; }

    /// <summary>Whether this row offers a close button - the tab's own effective answer (the owner shows close buttons
    /// AND this tab is closable).</summary>
    public bool CanClose { get; }

    /// <summary>The same answer as <see cref="CanClose"/>, in the form the row template binds to. A row is a view of a
    /// tab, so it answers in the terms the view asks in.</summary>
    public Visibility CloseVisibility => CanClose ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The owner's close-button look, handed on so a flyout row's × is the SAME button as a tab's - hover and
    /// all. Drawn by hand in the row template it was a bare glyph that never lit up, so nothing said it could be
    /// pressed; and a theme that restyles the close button would have had to do it twice.</summary>
    public ControlTemplate CloseButtonTemplate => _owner?.CloseButtonTemplate;

    /// <summary>Closes the tab this row stands for, through the same path a click on the tab's own × takes - so a
    /// <see cref="TabControl.TabCloseRequested"/> handler can veto it exactly as it would there.</summary>
    public ICommand Close { get; }

    private sealed class CloseTabCommand : ICommand
    {
        private readonly TabOverflowItem _row;

        public CloseTabCommand(TabOverflowItem row) => _row = row;

        public bool CanExecute(object parameter = null) => _row.CanClose;

        // By ITEM: a row usually has no container to hand over, and asking for one made the × a no-op - the click then
        // fell through to the row and SELECTED the tab.
        public void Execute(object parameter = null) => _row._owner?.RequestCloseItem(_row._item);

        public event EventHandler CanExecuteChanged;

        // A row is built fresh every time the flyout opens and lives only while it is up, so what it may do cannot change
        // underneath it. Kept on the contract, raised by nobody.
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
