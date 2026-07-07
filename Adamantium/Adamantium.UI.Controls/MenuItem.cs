using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// A single clickable row in a <see cref="ContextMenu"/> (or, later, a menu bar). Reuses <see cref="ButtonBase"/> for the
/// click + <see cref="ButtonBase.Command"/> plumbing; adds an optional <see cref="Icon"/> and right-aligned
/// <see cref="InputGestureText"/> (a shortcut hint). The row's label is the inherited <see cref="ContentControl.Content"/>.
/// This is a LEAF item (no nested sub-menu yet - a follow-up).
/// </summary>
public class MenuItem : ButtonBase
{
    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(MenuItem), new PropertyMetadata(null));

    public static readonly AdamantiumProperty InputGestureTextProperty = AdamantiumProperty.Register(nameof(InputGestureText),
        typeof(string), typeof(MenuItem), new PropertyMetadata(null));

    static MenuItem()
    {
        // A menu row is a keyboard-focus target (arrow-key navigation) - opt in, since the base default is false.
        FocusableProperty.OverrideMetadata(typeof(MenuItem), new PropertyMetadata(true));
    }

    /// <summary>Optional icon/glyph shown at the left of the row.</summary>
    public object Icon
    {
        get => GetValue<object>(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Optional shortcut hint shown right-aligned (e.g. "Ctrl+S"). Display only - it wires nothing.</summary>
    public string InputGestureText
    {
        get => GetValue<string>(InputGestureTextProperty);
        set => SetValue(InputGestureTextProperty, value);
    }
}
