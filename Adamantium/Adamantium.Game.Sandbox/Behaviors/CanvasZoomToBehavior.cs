using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.Input;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>A double click in a list of what is on the plane takes the camera to that thing.
/// <code>&lt;ListBox.Behaviors&gt;&lt;local:CanvasZoomToBehavior Canvas="{Binding ElementName=Canvas}"/&gt;&lt;/ListBox.Behaviors&gt;</code>
/// </summary>
/// <remarks>
/// A behavior because going somewhere is a CAMERA operation and only the canvas has one; a view model holding Scale and
/// Offset cannot centre on anything without knowing where the usable middle is once a docked panel has taken an edge.
/// Double click is read as a press with a click COUNT of two - there is no separate event for it.
/// </remarks>
public class CanvasZoomToBehavior : Behavior<ListBox>
{
    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasZoomToBehavior), new PropertyMetadata(null));

    /// <summary>The canvas this list belongs to.</summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    protected override void OnAttached(ListBox list) => list.MouseDown += OnPressed;

    protected override void OnDetached(ListBox list) => list.MouseDown -= OnPressed;

    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || e.ChangedButton != MouseButtons.Left) return;
        if (Canvas is not { } canvas || sender is not ListBox list) return;
        if (list.SelectedItem is not ICanvasItem item) return;

        canvas.ZoomTo(item);
        e.Handled = true;
    }
}
