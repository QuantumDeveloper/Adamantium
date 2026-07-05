using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.Game.Sandbox;

/// <summary>
/// View-layer behavior: on click, swaps the CURRENT theme's accent SEED (<c>Theme.AccentColor</c>). The theme derives the
/// whole ramp (hover/pressed) and a contrast-correct on-accent text colour from that one seed, and every consumer via
/// <c>{ThemeResource Accent*}</c> refreshes live - no theme reload. Attach in markup:
/// <code>&lt;Button&gt;&lt;Button.Behaviors&gt;&lt;local:CycleAccentBehavior/&gt;&lt;/Button.Behaviors&gt;&lt;/Button&gt;</code>
/// </summary>
public class CycleAccentBehavior : Behavior<Button>
{
    // Just seeds - the theme derives the hover/pressed ramp and the on-accent text colour for each.
    private static readonly string[] Seeds = ["#0091F7", "#107C10", "#8764B8", "#CA5010", "#C42B72"];

    private Button _button;
    private int _index;

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
        var theme = UIAppContext.Current?.ThemeManager?.CurrentTheme;
        if (theme == null) return;

        _index = (_index + 1) % Seeds.Length;
        theme.AccentColor = new SolidColorBrush(Seeds[_index]);
    }
}
