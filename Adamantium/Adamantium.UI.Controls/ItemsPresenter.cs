using System.Collections.Specialized;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// Bridge inside an <see cref="ItemsControl"/>'s template (named <c>PART_ItemsPresenter</c>): instantiates the
/// control's <c>ItemsPanel</c>, hosts it, and fills it with item containers. A non-virtualizing panel gets ALL
/// containers added to its <see cref="Panel.Children"/>; a <c>VirtualizingPanel</c> (later phase) self-realizes only the
/// visible window. For now this is the non-virtualizing path.
/// </summary>
public class ItemsPresenter : InputUIComponent
{
    private ItemsControl _owner;
    private Panel _panel;

    /// <summary>The items host panel (for the owning control / tests).</summary>
    internal Panel Panel => _panel;

    private bool IsVirtualizing => _panel is VirtualizingPanel;

    /// <summary>Called by the owning <see cref="ItemsControl"/> from OnApplyTemplate: builds the panel and fills it.</summary>
    internal void Connect(ItemsControl owner)
    {
        _owner = owner;
        Rebuild();
    }

    /// <summary>Rebuilds the items panel (e.g. the ItemsPanel template changed) and refills it.</summary>
    internal void Rebuild()
    {
        if (_panel != null)
        {
            RemoveVisualChild(_panel);
            RemoveLogicalChild(_panel);
            _panel = null;
        }

        if (_owner == null) return;

        var template = _owner.ItemsPanel ?? ItemsPanelTemplate.Default;
        _panel = template.Build(_owner).RootComponent as Panel ?? new StackPanel { Orientation = Orientation.Vertical };
        AddVisualChild(_panel);
        AddLogicalChild(_panel);

        if (_panel is VirtualizingPanel virtualizing)
            virtualizing.AttachOwner(_owner);   // panel self-realizes only the visible window
        else
            Refresh();                          // non-virtualizing panel: realize every container up front

        // The new panel holds the SAME containers, re-parented - and a container carries its measure state across, so it
        // keeps the size it worked out in the OLD panel and nothing asks it again. Any dirty mark it raised meanwhile is
        // gone too: the dirty queue belongs to the visual root, and re-attaching re-registers nothing. So say it here.
        // Measured: swapping a folded tab strip to a vertical panel left every tab at the 78x29 it had lying flat, while
        // its label was already turned on its side and drawn outside those bounds.
        foreach (var child in _panel.Children)
        {
            if (child is IMeasurableComponent measurable) measurable.InvalidateMeasure();
        }

        // The ANCESTORS as well, not just this presenter: every cached size above was computed from the panel that has just
        // been thrown away. Relying on the manager to carry the change up fails whenever the rebuild happened while this
        // subtree was detached - the propagation has no queue to travel through, and afterwards each ancestor is
        // "valid" holding a number that describes a panel which no longer exists.
        // Measured: a folded tool strip stayed 239x29 - a row of flat tabs - while its panel was already vertical and its
        // tabs 41x66, because the scroller two levels up had been measured in between and never asked again.
        for (IUIComponent node = this; node != null; node = node.VisualParent)
        {
            if (node is IMeasurableComponent ancestor) ancestor.InvalidateMeasure();
        }
    }

    /// <summary>Regenerates the item containers into the panel. Phase 1: full, non-virtualizing realization.</summary>
    internal void Refresh()
    {
        if (_owner == null || _panel == null) return;

        _panel.Children.Clear();
        var generator = _owner.ItemContainerGenerator;
        generator.Clear();

        var count = _owner.Items.Count;
        for (var i = 0; i < count; i++)
        {
            if (generator.Realize(i) is IMeasurableComponent container)
                _panel.Children.Add(container);
        }
    }

    /// <summary>Drops and re-generates all containers (ItemTemplate / ItemTemplateSelector / ItemContainerStyle changed).</summary>
    internal void RegenerateContainers()
    {
        if (_owner == null || _panel == null) return;
        if (_panel is VirtualizingPanel virtualizing)
            virtualizing.Revirtualize();   // clears the realized window + generator; next measure rebuilds with new settings
        else
            Refresh();
    }

    /// <summary>Applies a collection change incrementally so unaffected containers keep their state (non-virtualizing path).</summary>
    internal void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_owner == null || _panel == null) return;

        // Virtualizing panel: patch the realized window IN PLACE (reindex + rebind only the changed slots), so unchanged
        // containers keep their state instead of the whole list being torn down and rebuilt on every add/remove.
        if (_panel is VirtualizingPanel virtualizing)
        {
            virtualizing.OnItemsChanged(e);
            return;
        }

        var generator = _owner.ItemContainerGenerator;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                generator.OnItemsInserted(e.NewStartingIndex, e.NewItems.Count);
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    var index = e.NewStartingIndex + i;
                    if (generator.Realize(index) is IMeasurableComponent container)
                        _panel.Children.Insert(index, container);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                for (var i = e.OldItems.Count - 1; i >= 0; i--)
                {
                    var index = e.OldStartingIndex + i;
                    if (generator.ContainerFromIndex(index) is IMeasurableComponent container)
                        _panel.Children.Remove(container);
                    generator.Recycle(index);
                }
                generator.OnItemsRemoved(e.OldStartingIndex, e.OldItems.Count);
                break;

            case NotifyCollectionChangedAction.Replace:
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    var index = e.OldStartingIndex + i;
                    if (generator.ContainerFromIndex(index) is IMeasurableComponent old)
                        _panel.Children.Remove(old);
                    generator.Recycle(index);
                    if (generator.Realize(index) is IMeasurableComponent fresh)
                        _panel.Children.Insert(index, fresh);
                }
                break;

            default: // Reset (and Move, handled as a rebuild)
                Refresh();
                break;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_panel is not IMeasurableComponent measurable) return new Size();
        measurable.Measure(availableSize);
        return measurable.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_panel is IMeasurableComponent measurable)
            measurable.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
