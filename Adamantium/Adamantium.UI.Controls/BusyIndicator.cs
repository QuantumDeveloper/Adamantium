using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// An indeterminate "something is happening" indicator: it has no progress value, only <see cref="IsActive"/>.
/// </summary>
/// <remarks>
/// The control carries NO look of its own. Every indicator in the pack - the spinning ring, the bouncing dots, the
/// indeterminate bar, the ripple - is a THEME template selected by class (<c>&lt;BusyIndicator Classes="Dots"/&gt;</c>),
/// exactly as <see cref="ProgressBar"/> picks its ring template; and each template starts/stops its own animation off
/// <see cref="IsActive"/>. So a new look is a new style, not a new enum value here, and an app can supply its own without
/// touching the engine.
///
/// Turning it OFF must actually stop the animation: a looping animation never ends by itself, so a template's IsActive
/// trigger pairs its RunAnimationAction with a StopAnimationAction (see BusyIndicatorStyleSet).
/// </remarks>
public class BusyIndicator : TemplatedUIComponent
{
    public static readonly AdamantiumProperty IsActiveProperty = AdamantiumProperty.Register(nameof(IsActive),
        typeof(bool), typeof(BusyIndicator), new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender));

    /// <summary>Whether the indicator is running. False stops its animation and (per the theme) hides it.</summary>
    public bool IsActive
    {
        get => GetValue<bool>(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }
}
