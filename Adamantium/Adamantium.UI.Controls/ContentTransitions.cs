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
        // Nothing coming in is not nothing happening: content can be on its way (built off the loop thread) while what it
        // replaces still has to leave. What is leaving leaves either way; only the entrance is skipped.
        if (direction == ContentTransition.None || (incoming == null && outgoing == null))
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

        // A slide is a whole view translating as ONE thing, which is exactly what a render motion node is for: its subtree
        // rides its transform-table slot, so each frame of the slide costs one matrix instead of re-baking everything
        // under it, and ResolveScissor stops culling its units - the thing that decides whether content sliding INTO the
        // viewport has a draw recorded for it at all. Without this the whole scene was re-recorded on every frame of
        // every tab switch (measured: 261 frames of a 40 s run, ~60 ms each on a 22k-node tab).
        // Set BEFORE the animation starts, so its very first tick already takes the node route (see Transform.OnChanged),
        // and cleared when the slide ends so the view goes back to being ordinary content.
        AsMotionNode(incoming, true);
        AsMotionNode(outgoing, true);

        EnsureRenderTransform(incoming)?.BeginAnimation(axis,
            new DoubleAnimation { From = enterFrom, To = 0, Duration = duration }, () => AsMotionNode(incoming, false));

        var outTransform = EnsureRenderTransform(outgoing);
        if (outTransform != null)
        {
            outTransform.BeginAnimation(axis, new DoubleAnimation { From = 0, To = -enterFrom, Duration = duration },
                () => { AsMotionNode(outgoing, false); onOutgoingComplete?.Invoke(); });
        }
        else
        {
            AsMotionNode(outgoing, false);
            onOutgoingComplete?.Invoke();   // nothing (or nothing transformable) to slide out - drop it now
        }
    }

    // Promote/demote the sliding view. The mark is what re-freezes its layout entry: the snapshot records whether an
    // element is a motion node, and the draw side bakes its subtree node-relative or world-baked accordingly - so a flip
    // nobody announced would leave the two disagreeing about where everything is.
    //
    // A switch made DURING a slide drops the in-flight animation without firing its completion (AnimationManager.Begin),
    // so a view dropped at that moment keeps the flag. Left deliberately: being a motion node is a correct way to draw,
    // the next slide of that view demotes it at the end, and the alternative is bookkeeping across hosts that would
    // demote somebody else's slide mid-flight.
    private static void AsMotionNode(IUIComponent component, bool on)
    {
        if (component is not UIComponent ui || ui.IsRenderMotionNode == on) return;
        ui.IsRenderMotionNode = on;
        RenderDirty.MarkTransform(ui);
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
