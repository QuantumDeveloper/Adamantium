using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Engine.EntityServices;
using Adamantium.Multiverse.Input;

namespace Adamantium.Engine.Tools;

/// <summary>
/// A tool: a modifier or creator driven by the mouse, one in use at a time. A drag goes to the nearest of its handles; a
/// click anywhere else selects what is under the pointer. A selected light brings its own handles to whichever tool is in
/// use, so a light is moved and sized without switching tools.
/// </summary>
public abstract class ToolProcessor : EditorProcessor
{
    private readonly Handles[] own;
    private readonly Handles[] lightHandles = [new PointLightHandles(), new SpotLightHandles()];
    private readonly List<Handles> active = [];
    private Handles dragged;

    protected ToolProcessor(params Handles[] own)
    {
        this.own = own;
    }

    public override int Order => 100;

    /// <summary>How close to a line of a handle the pointer has to be to take it, in points: pixels at 100% scale.</summary>
    public float PickAperture { get; set; } = 6;

    public override void Update(AppTime appTime)
    {
        var target = Tools.Selection.Current;
        CollectActive(target);

        var cameras = Tools.Observatory.CurrentCameras;
        for (int i = 0; i < active.Count; i++)
        {
            for (int j = 0; j < cameras.Count; j++)
            {
                active[i].Place(target, cameras[j]);
            }
        }

        if (Tools.Observatory.PointerOutput is not { Camera: { } camera, Input: { CanLocatePointer: true } input })
        {
            Highlight(null, null);
            return;
        }

        var ray = PickRay.FromCamera(camera, input.RelativePosition);

        if (dragged != null)
        {
            if (input.IsMouseButtonDown(MouseButton.Left) && active.Contains(dragged))
            {
                dragged.Drag(target, ray);
                return;
            }

            dragged = null;
        }

        if (Tools.IsPointerTaken || input.IsMouseButtonDown(MouseButton.Right))
        {
            Highlight(null, null);
            return;
        }

        var hit = PickHandles(ray, PickAperture * camera.PixelsPerPoint, out var owner);
        Highlight(owner, hit.Entity);
        var under = hit.IsHit ? default : Tools.PickEntity(ray);
        Tools.Hovered = under.Entity;

        if (!input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        if (hit.IsHit)
        {
            dragged = owner;
            dragged.BeginDrag(target, hit.Entity, ray);
        }
        else
        {
            Tools.Selection.Current = under.Entity;
        }
    }

    public override void DrawOverlay(EditorOverlayProcessor overlay)
    {
        for (int i = 0; i < active.Count; i++)
        {
            active[i].Draw(overlay);
        }
    }

    protected override void OnDetached()
    {
        Highlight(null, null);
        dragged = null;
        active.Clear();
    }

    private void CollectActive(Entity target)
    {
        active.Clear();
        AddApplying(own, target);
        AddApplying(lightHandles, target);
    }

    private void AddApplying(Handles[] handles, Entity target)
    {
        for (int i = 0; i < handles.Length; i++)
        {
            if (handles[i].AppliesTo(target))
            {
                active.Add(handles[i]);
            }
        }
    }

    private PickHit PickHandles(in PickRay ray, float aperture, out Handles owner)
    {
        owner = null;
        var nearest = default(PickHit);
        for (int i = 0; i < active.Count; i++)
        {
            var hit = active[i].Pick(ray, aperture);
            if (hit.IsHit && (!nearest.IsHit || hit.Depth < nearest.Depth))
            {
                nearest = hit;
                owner = active[i];
            }
        }

        return nearest;
    }

    private void Highlight(Handles owner, Entity handle)
    {
        for (int i = 0; i < active.Count; i++)
        {
            active[i].Highlight(active[i] == owner ? handle : null);
        }
    }
}
