using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// View-layer behavior: on click, toggles the application's appearance between light and dark. Kept out of the
/// view-model (this is app-level UI, wired declaratively in markup):
/// <code>&lt;Button&gt;&lt;Button.Behaviors&gt;&lt;local:ToggleThemeBehavior/&gt;&lt;/Button.Behaviors&gt;&lt;/Button&gt;</code>
/// </summary>
/// <remarks>
/// It switches a VARIANT, not a theme, and that is the whole point: light and dark are two variants of one Fluent
/// theme, so the styles and templates on either side of this click are the same objects. Nothing is re-templated,
/// nothing is re-styled, and no element is written to - what changes is the colour inside about a hundred brushes the
/// elements are already holding. As two separate themes the same click rebuilt every template in the application.
/// </remarks>
public class ToggleThemeBehavior : Behavior<Button>
{
    private Button _button;

    protected override void OnAttached(Button button)
    {
        _button = button;
        button.Click += OnClick;
    }

    protected override void OnDetached(Button button)
    {
        button.Click -= OnClick;
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        var themeManager = UIAppContext.Current?.ThemeManager;
        if (themeManager?.CurrentTheme is not Adamantium.UI.Core.Resources.Theme theme) return;

        var next = theme.CurrentVariant == Adamantium.UI.Core.Resources.ThemeVariant.Dark
            ? Adamantium.UI.Core.Resources.ThemeVariant.Light
            : Adamantium.UI.Core.Resources.ThemeVariant.Dark;

        themeManager.SetVariant(next);
    }
}
