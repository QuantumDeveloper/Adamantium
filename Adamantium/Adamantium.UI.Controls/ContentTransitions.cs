using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.UI.Controls;

/// <summary>
/// Shared slide-transition logic for content hosts (<see cref="ContentControl"/> direct path and
/// <see cref="ContentPresenter"/> templated path). Animates the incoming content from off-screen to its place and,
/// when present, the outgoing content off-screen the opposite way, then notifies so the caller can drop the old one.
/// The host arranges both children at the same rect first; only the animated <c>RenderTransform</c> moves them.
/// </summary>
internal static class ContentTransitions
{
    /// <summary>
    /// Starts the slide. <paramref name="incoming"/> is required; <paramref name="outgoing"/> may be null (e.g. the
    /// first content, which just slides in). <paramref name="onOutgoingComplete"/> fires when the outgoing slide-out
    /// finishes (or immediately if there is nothing to slide out) so the caller removes the old content then.
    /// </summary>
    public static void Run(ContentTransition direction, double durationSeconds, Size size,
        IUIComponent incoming, IUIComponent outgoing, Action onOutgoingComplete)
    {
        if (direction == ContentTransition.None || incoming == null)
        {
            onOutgoingComplete?.Invoke();
            return;
        }

        var duration = TimeSpan.FromSeconds(Math.Max(0, durationSeconds));
        var horizontal = direction is ContentTransition.SlideLeft or ContentTransition.SlideRight;
        var distance = horizontal ? size.Width : size.Height;

        // SlideLeft/SlideUp: new enters from the positive side (right/below); SlideRight/SlideDown mirror it.
        var enterFrom = direction is ContentTransition.SlideLeft or ContentTransition.SlideUp ? distance : -distance;
        var axis = horizontal ? Transform.TranslateXProperty : Transform.TranslateYProperty;

        EnsureRenderTransform(incoming)?.BeginAnimation(axis, new DoubleAnimation { From = enterFrom, To = 0, Duration = duration });

        var outTransform = EnsureRenderTransform(outgoing);
        if (outTransform != null)
            outTransform.BeginAnimation(axis, new DoubleAnimation { From = 0, To = -enterFrom, Duration = duration }, onOutgoingComplete);
        else
            onOutgoingComplete?.Invoke();   // nothing (or nothing transformable) to slide out - drop it now
    }

    private static Transform EnsureRenderTransform(IUIComponent component)
    {
        if (component is not UIComponent ui)
            return null;

        if (ui.RenderTransform is not { } transform)
        {
            transform = new Transform();
            ui.RenderTransform = transform;
        }
        return transform;
    }
}
