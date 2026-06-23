using System.Collections.Generic;
using Adamantium.UI.Controls.Base;

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
    private HoverAdorner _hover;

    /// <summary>All active adorners (selection frames + the optional hover frame) as one flat list for the renderer.
    /// Selection first, hover last so the transient hover sits over a persistent selection.</summary>
    public IReadOnlyList<Adorner> Adorners
    {
        get
        {
            if (_hover == null) return _selection;
            var all = new List<Adorner>(_selection.Count + 1);
            all.AddRange(_selection);
            all.Add(_hover);
            return all;
        }
    }

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

    /// <summary>Clears both the selection and the hover frame.</summary>
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
