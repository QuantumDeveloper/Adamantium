using System;
using System.Collections;
using System.Collections.Generic;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>THE LIST OF NODE KINDS, opened where a wire was let go or where a node is being placed.
/// <para>Made of the canvas's own <see cref="InfiniteCanvas.NodeKinds"/> and nothing else: the catalogue is data, each
/// entry says which family it belongs to, and that is everything a palette needs. Picking one IS the answer - a list
/// opened by a gesture is answered by choosing from it - so the canvas puts the node down and closes the list.</para>
/// <para>The narrowing is here rather than in the application because it is about the WORDS the catalogue already
/// carries, not about what they mean.</para></summary>
public class CanvasNodePalette : Control, ICanvasPart
{
    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasNodePalette), new PropertyMetadata(null, OnCanvasChanged));

    /// <summary>What was typed to narrow the list. Empty shows everything.</summary>
    public static readonly AdamantiumProperty SearchProperty = AdamantiumProperty.Register(nameof(Search),
        typeof(String), typeof(CanvasNodePalette),
        new PropertyMetadata(String.Empty, PropertyMetadataOptions.BindsTwoWayByDefault, OnSearchChanged));

    /// <summary>The families to show, already narrowed - see <see cref="CanvasNodeGroup"/>.</summary>
    public static readonly AdamantiumProperty GroupsProperty = AdamantiumProperty.Register(nameof(Groups),
        typeof(IEnumerable), typeof(CanvasNodePalette), new PropertyMetadata(null));

    /// <summary>Whether anything has been typed - what the field's own clear button is shown by.</summary>
    public static readonly AdamantiumProperty HasSearchProperty = AdamantiumProperty.Register(nameof(HasSearch),
        typeof(Boolean), typeof(CanvasNodePalette), new PropertyMetadata(false));

    /// <summary>What was picked out of the list - an entry of the catalogue, which the palette hands straight to the
    /// canvas. PICKING IS THE ANSWER: a list opened by a gesture is answered by choosing from it.
    /// <para>The palette carries this rather than the list writing into the canvas through it: a two-way binding whose
    /// path goes THROUGH another object (<c>Canvas.PickedKind</c>) never wrote back, so every pick was silently
    /// dropped - the row lit up and no node appeared.</para></summary>
    public static readonly AdamantiumProperty PickedKindProperty = AdamantiumProperty.Register(nameof(PickedKind),
        typeof(ICanvasNodeKind), typeof(CanvasNodePalette),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnPickedKindChanged));

    public ICanvasNodeKind PickedKind
    {
        get => GetValue<ICanvasNodeKind>(PickedKindProperty);
        set => SetValue(PickedKindProperty, value);
    }

    /// <summary>The plane this palette puts nodes on.</summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    public String Search
    {
        get => GetValue<String>(SearchProperty);
        set => SetValue(SearchProperty, value);
    }

    public IEnumerable Groups
    {
        get => GetValue<IEnumerable>(GroupsProperty);
        private set => SetCurrentValue(GroupsProperty, value);
    }

    public Boolean HasSearch
    {
        get => GetValue<Boolean>(HasSearchProperty);
        private set => SetCurrentValue(HasSearchProperty, value);
    }

    private CanvasCommand _clear;
    private CanvasCommand _close;

    /// <summary>Empties the search field.</summary>
    public CanvasCommand ClearSearchCommand => _clear ??= new CanvasCommand(_ => Cleared(), _ => HasSearch);

    /// <summary>Puts the list away without choosing anything.</summary>
    public CanvasCommand CloseCommand => _close ??= new CanvasCommand(_ => Close());

    // A CURRENT value and not a Local one: what a person types lives in the field's own two-way binding, and a command
    // answering in the Local slot would mask that binding - and any trigger a theme puts here - from then on.
    private void Cleared() => SetCurrentValue(SearchProperty, String.Empty);

    private void Close()
    {
        if (Canvas is { } canvas) canvas.SetCurrentValue(InfiniteCanvas.IsPaletteOpenProperty, false);

        Cleared();
    }

    // One answer at a time: handing the kind on writes here again to let the row go, and that must not read as a
    // second pick.
    private bool _answering;

    private static void OnPickedKindChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasNodePalette palette || palette._answering) return;
        if (e.NewValue is not ICanvasNodeKind picked) return;

        palette._answering = true;
        try
        {
            if (palette.Canvas is { } canvas)
            {
                canvas.SetCurrentValue(InfiniteCanvas.PickedKindProperty, picked);

                // ...and the canvas lets go of it too, or the SAME kind picked a second time is not a change and the
                // list answers nothing at all.
                canvas.SetCurrentValue(InfiniteCanvas.PickedKindProperty, null);
            }

            palette.SetCurrentValue(PickedKindProperty, null);
        }
        finally
        {
            palette._answering = false;
        }
    }

    private static void OnCanvasChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasNodePalette palette) return;

        if (e.OldValue is InfiniteCanvas was) was.NodeKindsChanged -= palette.OnKindsChanged;
        if (e.NewValue is InfiniteCanvas now) now.NodeKindsChanged += palette.OnKindsChanged;

        palette.Gather();
    }

    private void OnKindsChanged(object sender, EventArgs e) => Gather();

    private static void OnSearchChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasNodePalette palette) return;

        palette.HasSearch = !String.IsNullOrEmpty(palette.Search);
        palette.Gather();
    }

    // THE WHOLE LIST, in sections, narrowed by what has been typed. Rebuilt rather than filtered in place: the list is
    // a catalogue of a few dozen entries read once when the palette opens, and a rebuilt list is one that cannot go
    // stale.
    private void Gather()
    {
        if (Canvas is not { NodeKinds: { } kinds })
        {
            Groups = null;
            return;
        }

        var wanted = Search?.Trim();
        var sections = new List<CanvasNodeGroup>();
        var found = new Dictionary<string, List<ICanvasNodeKind>>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var entry in kinds)
        {
            if (entry is not ICanvasNodeKind kind || !Matches(kind, wanted)) continue;

            // A kind with no family of its own stands under its own name rather than under a section called nothing.
            var title = String.IsNullOrWhiteSpace(kind.Group) ? kind.Title : kind.Group;

            if (!found.TryGetValue(title, out var family))
            {
                family = new List<ICanvasNodeKind>();
                found.Add(title, family);
                sections.Add(new CanvasNodeGroup(title, family));
            }

            family.Add(kind);
        }

        Groups = sections;
    }

    private static bool Matches(ICanvasNodeKind kind, string wanted) =>
        String.IsNullOrEmpty(wanted)
        || (kind.Title ?? String.Empty).Contains(wanted, StringComparison.CurrentCultureIgnoreCase)
        || (kind.Kind ?? String.Empty).Contains(wanted, StringComparison.CurrentCultureIgnoreCase)
        || (kind.Group ?? String.Empty).Contains(wanted, StringComparison.CurrentCultureIgnoreCase);
}
