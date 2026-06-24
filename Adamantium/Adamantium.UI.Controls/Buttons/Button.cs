using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Buttons;

/// <summary>A standard clickable button. Click/command/press behaviour lives in <see cref="ButtonBase"/>.</summary>
public class Button : ButtonBase
{
    public static readonly AdamantiumProperty IsDefaultProperty = AdamantiumProperty.Register(nameof(IsDefault),
        typeof(bool), typeof(Button), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsCancelProperty = AdamantiumProperty.Register(nameof(IsCancel),
        typeof(bool), typeof(Button), new PropertyMetadata(false));

    /// <summary>When true, Enter (unhandled by the focused element) activates this button - the window's default button.</summary>
    public bool IsDefault
    {
        get => GetValue<bool>(IsDefaultProperty);
        set => SetValue(IsDefaultProperty, value);
    }

    /// <summary>When true, Escape activates this button - the window's cancel button.</summary>
    public bool IsCancel
    {
        get => GetValue<bool>(IsCancelProperty);
        set => SetValue(IsCancelProperty, value);
    }
}
