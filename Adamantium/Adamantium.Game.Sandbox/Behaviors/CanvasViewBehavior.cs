using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>What the view bar's buttons do to the camera.
/// <code>&lt;Button&gt;&lt;Button.Behaviors&gt;&lt;local:CanvasViewBehavior Canvas="{Binding ElementName=Canvas}" Does="ZoomIn"/&gt;&lt;/Button.Behaviors&gt;&lt;/Button&gt;</code>
/// </summary>
/// <remarks>
/// Why a behavior. Zooming about the middle of the viewport and going home are CAMERA operations - the canvas has them
/// as methods, and it must, because only it knows where the middle is once a docked panel has taken part of the edge.
/// A view model cannot reach them: it holds Scale and Offset, and writing Scale by hand zooms about the world's origin,
/// which is wherever it happens to be and usually off screen. So the button is wired to the control, the way every
/// other no-code-behind wiring in this application is.
/// </remarks>
public class CanvasViewBehavior : Behavior<ButtonBase>
{
    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasViewBehavior), new PropertyMetadata(null));

    public static readonly AdamantiumProperty DoesProperty = AdamantiumProperty.Register(nameof(Does),
        typeof(CanvasViewAction), typeof(CanvasViewBehavior), new PropertyMetadata(CanvasViewAction.ZoomIn));

    public static readonly AdamantiumProperty StepProperty = AdamantiumProperty.Register(nameof(Step),
        typeof(double), typeof(CanvasViewBehavior), new PropertyMetadata(1.5));

    /// <summary>The canvas this button steers.</summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    /// <summary>Which of the three it does.</summary>
    public CanvasViewAction Does
    {
        get => GetValue<CanvasViewAction>(DoesProperty);
        set => SetValue(DoesProperty, value);
    }

    /// <summary>How much one press zooms by.</summary>
    public double Step
    {
        get => GetValue<double>(StepProperty);
        set => SetValue(StepProperty, value);
    }

    protected override void OnAttached(ButtonBase button) => button.Click += OnClick;

    protected override void OnDetached(ButtonBase button) => button.Click -= OnClick;

    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (Canvas is not { } canvas) return;

        switch (Does)
        {
            case CanvasViewAction.ZoomIn:
                canvas.ZoomBy(Step);
                break;

            case CanvasViewAction.ZoomOut:
                canvas.ZoomBy(1 / Step);
                break;

            case CanvasViewAction.Home:
                canvas.ResetCamera();
                break;

            case CanvasViewAction.Delete:
                canvas.RequestDeleteSelection();
                break;

            case CanvasViewAction.Group:
                canvas.GroupSelection();
                break;

            case CanvasViewAction.Ungroup:
                canvas.UngroupSelection();
                break;

            case CanvasViewAction.Undo:
                canvas.Undo();
                break;

            case CanvasViewAction.Redo:
                canvas.Redo();
                break;
        }
    }
}

/// <summary>What a <see cref="CanvasViewBehavior"/> button does.</summary>
public enum CanvasViewAction
{
    ZoomIn,
    ZoomOut,
    Home,

    /// <summary>Asks the canvas to take the selection out. Through the canvas rather than through the view model, so
    /// that the button and the Delete key meet the same question.</summary>
    Delete,

    /// <summary>Makes one thing out of what is selected, and breaks one open again. On the canvas because grouping
    /// moves things in and out of the scene in PAINT ORDER, which only the canvas and the scene can say.</summary>
    Group,

    Ungroup,

    /// <summary>Puts the last step back, and takes it forward again. On the canvas because only the canvas knows where
    /// one step ends and the next begins.</summary>
    Undo,

    Redo
}
