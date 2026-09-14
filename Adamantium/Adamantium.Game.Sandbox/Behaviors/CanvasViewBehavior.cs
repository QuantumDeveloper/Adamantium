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
        }
    }
}

/// <summary>What a <see cref="CanvasViewBehavior"/> button does.</summary>
public enum CanvasViewAction
{
    ZoomIn,
    ZoomOut,
    Home
}
