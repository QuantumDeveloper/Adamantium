using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls;

/// <summary>
/// The drag grip (⣿) you put in a row so only IT starts the drag - the rest of the row stays clickable, selectable,
/// editable. Drop it into an item template next to the content and the drag engine finds it; nothing else to wire up:
/// <code>&lt;DragHandle IsActive="{Binding HandleOnlyDrag}"/&gt;</code>
/// The look (the dot cluster, the hover highlight) is a <c>ControlTemplate</c> from the active theme - restyle it there.
/// For an element that is NOT this control (a glyph, an icon), the attached <c>DragDrop.IsDragHandle</c> does the same.
/// </summary>
public class DragHandle : Control, IDragHandle
{
    public DragHandle()
    {
        Cursor = Cursors.SizeAll;   // the grip advertises itself before the press
    }

    /// <summary>Whether the grip is in force. False and the source drags by its whole body again - bind a "drag only by
    /// the handle" switch here rather than hiding the control, so the grip stays where the eye expects it.</summary>
    public static readonly AdamantiumProperty IsActiveProperty = AdamantiumProperty.Register(nameof(IsActive),
        typeof(bool), typeof(DragHandle), new PropertyMetadata(true));

    public bool IsActive
    {
        get => GetValue<bool>(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    bool IDragHandle.IsDragHandleActive => IsActive;
}
