using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// What <c>x:Load</c> leaves in place of an element that is not built yet: the factory that would build it and the
/// condition that says when. While the condition is false NOTHING under it exists - this is an absent element, not a
/// hidden one.
/// <para>The slot is a LOGICAL child only, never a visual one: layout and rendering never see it, so the container has
/// no placeholder to skip over. It has to be in the logical tree at all, rather than waiting outside like the element
/// it holds, because a condition may be a binding - and a binding resolves against the DataContext in force AT THE
/// ELEMENT'S PLACE. Nothing outside the tree has one.</para>
/// <para>Inserting and removing belong to whoever generated the markup: a panel adds to Children, a decorator sets
/// Child, a content control sets Content. The slot is handed both as closures rather than guessing the shape.</para>
/// </summary>
public sealed class LoadSlot : FundamentalUIComponent
{
    public static readonly AdamantiumProperty ConditionProperty =
        AdamantiumProperty.Register(nameof(Condition), typeof(bool), typeof(LoadSlot),
            new PropertyMetadata(false, OnConditionChanged));

    private readonly Func<IUIComponent> _build;
    private readonly Action<IUIComponent> _insert;
    private readonly Action<IUIComponent> _remove;

    private IUIComponent _element;

    public LoadSlot(Func<IUIComponent> build, Action<IUIComponent> insert, Action<IUIComponent> remove)
    {
        _build = build;
        _insert = insert;
        _remove = remove;
    }

    /// <summary>The common case: the element is one of a container's authored children, and goes back at the index it
    /// was written at rather than on the end. Uses the container's own incremental child editing - the same contract the
    /// live designer reconciles markup edits through - so nothing here knows whether it is a panel, a decorator or an
    /// items control.</summary>
    public LoadSlot(Func<IUIComponent> build, IContainer container, int index)
        : this(build, child => container.InsertChildComponent(index, child), child => Remove(container, child))
    {
    }

    /// <summary>Whether the element should be in the tree. Bindable, which is the whole reason this object exists.</summary>
    public bool Condition
    {
        get => GetValue<bool>(ConditionProperty);
        set => SetValue(ConditionProperty, value);
    }

    /// <summary>True once the element has been built - it stays built after that, even when the condition turns false
    /// again (then it is parked, not destroyed).</summary>
    public bool IsBuilt => _element != null;

    /// <summary>The element, BUILT AND INSERTED if it was not there yet. This is what asking for it by name does: in
    /// UWP the generated field is simply null until something loads it, and everyone trips over that - here "take it by
    /// name" and "load it" are one action.</summary>
    public IUIComponent Element
    {
        get
        {
            Load();
            return _element;
        }
    }

    /// <summary>The slot is leaving with its view. Whatever it is holding out of the tree is holding a parked mark, and
    /// a parked mark means "coming back" - the renderer keeps that subtree's units on the strength of it. Nothing can
    /// ever show this one again, so the mark has to go, or the units (and the subtree behind them) stay for good.</summary>
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);

        if (_element is { VisualParent: null })
        {
            ParkedSubtree.Revalidate(_element);
        }
    }

    private static void OnConditionChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        var slot = (LoadSlot)component;
        if (slot.Condition)
        {
            slot.Load();
        }
        else
        {
            slot.Unload();
        }
    }

    private void Load()
    {
        if (_element == null)
        {
            _element = _build();
            _insert(_element);
            return;
        }

        // Built before and parked since: it comes back as it was, which is the same return a kept view makes.
        if (_element.VisualParent == null)
        {
            _insert(_element);
            ParkedSubtree.Unpark(_element);
        }
    }

    private void Unload()
    {
        if (_element == null || _element.VisualParent == null) return;

        // Marked BEFORE it is removed, so the removal reads as "coming back" and the renderer keeps what it built -
        // the same contract ParkedVisuals relies on.
        ParkedSubtree.Park(_element);
        _remove(_element);

        // IContainer's incremental editing has do-nothing defaults, so a container that never overrode them would leave
        // the element on screen while the condition says it is gone. Say so instead of showing the wrong thing.
        if (_element.VisualParent != null)
        {
            throw new InvalidOperationException(
                $"x:Load cannot unload from {_element.VisualParent.GetType().Name}: it does not implement removing a " +
                "single child (IContainer.RemoveChildComponentAt).");
        }
    }

    private static void Remove(IContainer container, IUIComponent child)
    {
        var children = container.GetChildComponents();
        for (var i = 0; i < children.Count; i++)
        {
            if (ReferenceEquals(children[i], child))
            {
                container.RemoveChildComponentAt(i);
                return;
            }
        }
    }
}
