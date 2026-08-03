using System.Collections.Generic;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// Holds the active adorners for a window and is rendered as a separate stage ON TOP of the content (it is NOT part of
/// the content visual tree, so it never pollutes the user's tree and can be toggled). The designer drives it via
/// <see cref="SetSelection"/> (persistent, on click) and <see cref="SetHover"/> (transient, on mouse move); the renderer
/// consumes <see cref="Adorners"/> as a flat list of visuals to draw last. Both the selection and hover frames are drawn
/// by the framework (stroke-aware), so the designer host never draws its own.
/// </summary>
public class AdornerLayer
{
    private readonly List<Adorner> _selection = [];
    private readonly List<IUIComponent> _overlays = [];   // general overlays (e.g. the templatable drop-insertion indicator)
    private HoverAdorner _hover;
    private FocusAdorner _focus;

    /// <summary>All active overlays (selection frames + general overlays + the hover frame + the focus ring) as one flat
    /// list for the renderer. Selection first, overlays next, then the transient hover, and the focus ring on top - it
    /// says where the keyboard is, which is the one thing that must not be buried under anything else.</summary>
    public IReadOnlyList<IUIComponent> Adorners
    {
        get
        {
            var all = new List<IUIComponent>(_selection.Count + _overlays.Count + 2);
            all.AddRange(_selection);
            all.AddRange(_overlays);
            if (_hover != null) all.Add(_hover);
            if (_focus != null) all.Add(_focus);
            return all;
        }
    }

    /// <summary>Shows the focus ring on <paramref name="element"/>; null clears it. One ring per window, replaced as the
    /// focus moves - the same shape the transient hover frame has.</summary>
    public void SetFocus(UIComponent element)
    {
        if (element == null)
        {
            _focus = null;
            return;
        }

        if (_focus != null && ReferenceEquals(_focus.AdornedElement, element))
            return;

        _focus = new FocusAdorner(element);
        // The control's own ring, where it asked for one. Attached here rather than left to the stage's ApplyTheme:
        // attached styles are applied AFTER the theme, so a control's FocusVisualStyle overrides the theme's ring
        // instead of being overwritten by it.
        if (element is InputUIComponent { FocusVisualStyle: { } style })
            _focus.AttachStyles(style);
    }

    /// <summary>Add a general overlay (a raw Adorner or a templatable one), rendered on top of the content. Idempotent.</summary>
    public void Add(IUIComponent overlay)
    {
        if (overlay != null && !_overlays.Contains(overlay)) _overlays.Add(overlay);
    }

    /// <summary>Remove a previously added overlay.</summary>
    public void Remove(IUIComponent overlay) => _overlays.Remove(overlay);

    /// <summary>Replaces the persistent selection with one frame per element (the designer calls this on click).
    /// Null/empty clears the selection.</summary>
    public void SetSelection(IEnumerable<UIComponent> elements)
    {
        _selection.Clear();
        if (elements == null) return;
        foreach (var element in elements)
            if (element != null) _selection.Add(new SelectionAdorner(element));
    }

    /// <summary>Sets the transient hover frame (the designer calls this on mouse move); null clears it. Skipped when the
    /// element is already selected, so a hovered+selected element isn't double-framed.</summary>
    public void SetHover(UIComponent element)
    {
        _hover = element != null && !IsSelected(element) ? new HoverAdorner(element) : null;
    }

    /// <summary>Clears the selection and the hover frame. The focus ring is NOT cleared here: it is not designer
    /// decoration but where the keyboard currently is, and that does not stop being true when a selection changes.</summary>
    public void Clear()
    {
        _selection.Clear();
        _hover = null;
    }

    private bool IsSelected(UIComponent element)
    {
        foreach (var a in _selection)
            if (ReferenceEquals(a.AdornedElement, element)) return true;
        return false;
    }
}
