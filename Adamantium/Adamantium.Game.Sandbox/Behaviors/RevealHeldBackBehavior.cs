using System.Linq;
using Adamantium.Game.Sandbox.Views;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// The other half of <c>x:Load</c>: an element held back by <c>x:Load="False"</c> waits for someone to ASK for it, and
/// with no code behind a view the asker is a behavior. Reading the name IS the asking - the generated accessor builds
/// the element and puts it back at its place.
/// </summary>
public class RevealHeldBackBehavior : Behavior<Button>
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
        var view = _button.GetSelfAndLogicalAncestors().OfType<MarkupView>().FirstOrDefault();
        if (view == null) return;

        _ = view.Manual;
    }
}
