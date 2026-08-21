using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.EntityServices;

/// <summary>
/// Whether an overlay STAGE - the popups, the adorners - could look different than it did last frame. A stage that
/// cannot say this rebuilds itself every frame: it walks its components, re-records their draws and re-rasterizes them,
/// for a picture identical to the one already on screen. Measured on an idle window: the adorner stage walked 2732 times
/// in 25 seconds to draw nothing at all.
/// <para>Asked of the flattened stage: the set of drawn components, whether any of them says its content is stale, and
/// whether any of them moved. That is far cheaper than the rebuild it decides against - and it is the same question for
/// every stage, which is why it lives in one place rather than once per processor.</para>
/// </summary>
internal sealed class OverlayRebuildGate
{
    private HashSet<Guid> _prevIds = new();
    private HashSet<Guid> _ids = new();   // swapped with _prevIds, so asking costs no allocation
    private readonly Dictionary<Guid, Vector3F> _prevPos = new();

    /// <summary>The glyph-arrival version this stage has already redrawn for. Per GATE: each overlay stage decides its
    /// own redraws, and the arrival is global.</summary>
    private int _seenGlyphVersion;

    public bool HasChanged(IReadOnlyList<IUIComponent> flat)
    {
        var changed = false;

        // LETTERS THAT LANDED are a change to what this stage draws, and until now the gate never asked. An overlay is
        // built by BuildFromComponents, which fuses record and apply and so never runs the content path's late-glyph
        // adoption - and the gate held the rebuild back because the open set, the geometry and the positions were all
        // unchanged. So a glyph that finished rasterizing AFTER a popup first opened had nothing to put it on screen:
        // a SlidePanel's close cross stayed blank until the panel was closed and opened again, which is what finally
        // moved the gate. Asked here because this is the one place that decides whether the stage redraws at all.
        var landed = Adamantium.Graphics.Fonts.FontAtlasStore.LandedVersion;
        if (landed != _seenGlyphVersion)
        {
            _seenGlyphVersion = landed;
            changed = true;
        }

        _ids.Clear();
        foreach (var component in flat)
        {
            _ids.Add(component.RenderId);
            if (!component.IsGeometryValid) changed = true;

            var position = component.WorldTransform.TranslationVector;
            if (_prevPos.TryGetValue(component.RenderId, out var previous) && previous.Equals(position)) continue;

            changed = true;
            _prevPos[component.RenderId] = position;
        }

        if (!changed && !_ids.SetEquals(_prevIds)) changed = true;

        if (changed)
        {
            // Forget what is no longer shown, or a stage that opens and closes things keeps growing a table of positions
            // nobody will ask about again.
            if (_prevPos.Count > _ids.Count)
            {
                _gone.Clear();
                foreach (var id in _prevPos.Keys)
                    if (!_ids.Contains(id)) _gone.Add(id);
                foreach (var id in _gone) _prevPos.Remove(id);
            }

            (_prevIds, _ids) = (_ids, _prevIds);
        }

        return changed;
    }

    private readonly List<Guid> _gone = new();
}
