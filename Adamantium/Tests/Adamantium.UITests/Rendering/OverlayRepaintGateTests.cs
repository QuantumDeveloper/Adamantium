using System.Collections.Generic;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.EntityServices;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// The overlay stages - popups, adorners - redraw only when their gate says they could look different. A RECOLOUR is
/// the change that leaves the commands, the geometry, the positions and the open set all identical, so the gate has to
/// hear about it separately or the stage replays a picture the trigger has already moved on from.
/// </summary>
[TestFixture]
public class OverlayRepaintGateTests
{
    private static void MakeGeometryValid(params UI.Controls.Base.UIComponent[] components)
    {
        foreach (var component in components) component.Render(new DrawingContext());
    }

    [Test]
    public void AnOpacityChangeOpensTheGate()
    {
        var scope = RenderDirtyRouter.NewScope();
        var root = new Border();
        var glyph = new Border();
        root.Child = glyph;
        root.ClaimRenderScope(scope);

        Assert.That(glyph.RenderScope, Is.SameAs(scope), "the claim must reach the child, or its marks go elsewhere");

        var flat = new List<IUIComponent> { root, glyph };
        MakeGeometryValid(root, glyph);

        var gate = new OverlayRebuildGate();
        gate.HasChanged(flat, scope);                       // first sight of this set - always a rebuild
        Assert.That(gate.HasChanged(flat, scope), Is.False, "nothing moved: the stage must not redraw");

        // The trigger's write. Same shape, same commands, same place - only the colour the shader composes.
        glyph.Opacity = 0;

        Assert.That(gate.HasChanged(flat, scope), Is.True,
            "a recolour inside the stage must open the gate, or the picture keeps the old opacity forever");
        Assert.That(gate.HasChanged(flat, scope), Is.False, "...and once redrawn, it settles again");
    }

    [Test]
    public void ARecolourInAnotherStageLeavesThisOneAlone()
    {
        var mine = RenderDirtyRouter.NewScope();
        var theirs = RenderDirtyRouter.NewScope();

        var root = new Border();
        root.ClaimRenderScope(mine);
        var stranger = new Border();
        stranger.ClaimRenderScope(theirs);

        var flat = new List<IUIComponent> { root };
        MakeGeometryValid(root, stranger);

        var gate = new OverlayRebuildGate();
        gate.HasChanged(flat, mine);
        Assert.That(gate.HasChanged(flat, mine), Is.False);

        stranger.Opacity = 0.5;

        Assert.That(gate.HasChanged(flat, mine), Is.False,
            "a hovered menu item is no reason for the adorner stage to redraw itself");
    }

    [Test]
    public void TheMarkSurvivesTheFrameClear()
    {
        var scope = RenderDirtyRouter.NewScope();
        var root = new Border();
        root.ClaimRenderScope(scope);

        var flat = new List<IUIComponent> { root };
        MakeGeometryValid(root);

        var gate = new OverlayRebuildGate();
        gate.HasChanged(flat, scope);
        Assert.That(gate.HasChanged(flat, scope), Is.False);

        root.Opacity = 0;

        // The loop thread wipes the mark SETS once per frame, and an overlay stage builds later, on the render thread.
        // Asking the sets directly is what made the defect invisible; the gate must survive this.
        scope.Clear();

        Assert.That(gate.HasChanged(flat, scope), Is.True,
            "the recolour happened - a clear by another thread must not swallow it");
    }
}
