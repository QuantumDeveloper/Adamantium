using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.Game.Sandbox;

/// <summary>
/// View-layer behavior: on click, toggles the application theme between FluentDark and FluentLight via the ThemeManager.
/// Kept out of the view-model (theme switching is app-level UI, wired declaratively in markup):
/// <code>&lt;Button&gt;&lt;Button.Behaviors&gt;&lt;local:ToggleThemeBehavior/&gt;&lt;/Button.Behaviors&gt;&lt;/Button&gt;</code>
/// </summary>
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
        if (themeManager == null) return;

        var nextName = themeManager.CurrentTheme?.Name == "FluentLight" ? "FluentDark" : "FluentLight";
        var next = themeManager[nextName];
        if (next != null) themeManager.SetTheme(next);
    }
}
