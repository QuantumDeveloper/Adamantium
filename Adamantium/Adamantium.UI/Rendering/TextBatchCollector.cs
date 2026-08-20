using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Text glyph batch (docs/TEXT_GLYPH_BATCH_PLAN.md §9 Stage 2): collects same-clip + same-atlas visible text blocks into
// ONE STORAGE-INSTANCED draw per segment. Each glyph is a per-instance GlyphItem (NODE-LOCAL rect + atlas UV + transform
// slot + color) in a BDA storage buffer; the glyph VS transforms it to world on the GPU via the transform table at its
// slot (FontEffect.fx pass RenderMsdfBatchInstanced), so there is NO per-glyph CPU world bake and a scrolling block moves
// by one table matrix write (node-aware). Foreground is per-instance, so many colors share the draw. Segment/buffer/
// overlap machinery is in BatchCollector; this adds glyph packing + the atlas-bound draw. Rendered ABOVE the rect batch.
internal sealed class TextBatchCollector : BatchCollector<GlyphItem>
{
    private FontAtlas _atlas;            // the pending segment's atlas (one bind per draw)
    private FontRenderer _fontRenderer;

    /// <summary>Device address of the owning cache's transform table - the glyph VS fetches each instance's node matrix
    /// from it by the instance's slot (set by RenderCache every frame; slot 0 is identity for world-baked glyphs).</summary>
    public ulong TransformsAddress { get; set; }

    // Per-segment atlas + renderer, parallel to the base segment list, so the clean-frame op replay can re-bind each
    // recorded segment's atlas (DrawSegment reads _atlas/_fontRenderer, which otherwise hold only the LAST segment's).
    private readonly List<(FontAtlas Atlas, FontRenderer Renderer)> _segState = new();

    public TextBatchCollector() : base(8192) { }

    protected override void OnBeginFrame(IGraphicsDevice device) => _segState.Clear();

    // The atlas the glyphs staged for this patch need. A text segment binds ONE atlas, so a repair that would put glyphs
    // of another one into it is refused rather than drawn with the wrong sheet.
    private FontAtlas _stagedAtlas;
    private FontRenderer _stagedRenderer;

    /// <summary>Bake a block's glyphs into the patch stage - the text answer to BatchArena.TryStage. This is the half a
    /// changed glyph COUNT needs: the in-place re-bake (UpdateRun) only ever covered a count that held steady, and
    /// anything that grew or shrank - a counter, a clock, an fps plate - fell through to a walk of the whole scene.</summary>
    public override bool TryStage(IRenderUnit unit, Matrix4x4F world, int transformSlot)
    {
        if (unit is not TextRenderUnit tru || tru.TextComponent is not { } tc) return false;
        if (!CanBatch(tc, out var atlas)) return false;
        if (_stagedAtlas != null && atlas != _stagedAtlas) return false;   // one patch, one sheet

        // The unit's OWN placement on top of the bake - a Drawing's text run sits at its own spot inside the element.
        // The walk folds it in the same way; without it the glyphs land at the element's origin instead of the run's.
        var placed = tru.Place(world);

        var first = Stage.Count;
        for (var i = 0; i < tc.GlyphRun.Count; i++) Stage.Add(default);
        if (!PackInto(tc, placed, transformSlot, CollectionsMarshal.AsSpan(Stage).Slice(first, tc.GlyphRun.Count)))
        {
            Stage.RemoveRange(first, Stage.Count - first);
            return false;
        }

        _stagedAtlas = atlas;
        _stagedRenderer = tc.FontRenderer;
        return true;
    }

    public override void ClearStage()
    {
        base.ClearStage();
        _stagedAtlas = null;
        _stagedRenderer = null;
    }

    // A repaired segment keeps drawing with the sheet it was recorded against, so the staged glyphs have to belong to it.
    private bool SegmentTakesStagedAtlas(int id)
    {
        var index = IndexOfSegment(id);
        if (index < 0 || index >= _segState.Count) return false;
        return _segState[index].Atlas == _stagedAtlas;
    }

    public override bool ReplaceStagedInSegment(IGraphicsDevice device, int id, int at, int replaced, int stageFirst, int stageCount)
        => SegmentTakesStagedAtlas(id) && base.ReplaceStagedInSegment(device, id, at, replaced, stageFirst, stageCount);

    public override bool RepointSegmentAroundStage(IGraphicsDevice device, int id, int first, int at, int replaced, int count,
        Rect2D scissor, int stageFirst, int stageCount)
        => SegmentTakesStagedAtlas(id)
           && base.RepointSegmentAroundStage(device, id, first, at, replaced, count, scissor, stageFirst, stageCount);

    public override int AllocateSegmentFromStage(IGraphicsDevice device, Rect2D scissor, int stageFirst, int stageCount)
    {
        // A brand-new segment has no sheet yet - it takes the staged one, which is what OnSegmentRecorded would have
        // written had a walk produced it.
        _atlas = _stagedAtlas;
        _fontRenderer = _stagedRenderer;
        return base.AllocateSegmentFromStage(device, scissor, stageFirst, stageCount);
    }

    protected override void OnSegmentRecorded(int index)
    {
        while (_segState.Count <= index) _segState.Add(default);
        _segState[index] = (_atlas, _fontRenderer);
    }

    protected override void BindSegment(int index)
    {
        var s = _segState[index];
        _atlas = s.Atlas;
        _fontRenderer = s.Renderer;
    }

    // Whether this block can batch at all (and its atlas). Canonical MSDF only - the batch pixel shader is the MSDF
    // variant; outline / gradient-AA / empty / non-solid-foreground text (and UseTextBatch=off) fall back to the
    // per-block direct draw. The clip-group check lives in RenderCache; the atlas check is SameAtlas below.
    public bool CanBatch(TextRenderComponent tc, out FontAtlas atlas)
    {
        atlas = null;
        if (!FontRenderer.UseTextBatch) return false;   // off -> every block falls back to the per-block direct draw
        var run = tc.GlyphRun;                            // the FROZEN glyph snapshot (not the live, reshaped-in-place layout)
        if (run == null || run.Count == 0 || run.Atlas == null) return false;
        var fr = tc.FontRenderer;
        if (fr == null || !fr.UseCanonicalMsdf || fr.UseOutline) return false;
        if (tc.Foreground is not SolidColorBrush) return false;
        atlas = run.Atlas;
        return true;
    }

    // Still the pending segment's atlas? (One draw binds one atlas; a change flushes both batches - see RenderCache.)
    public bool SameAtlas(FontAtlas atlas) => !Active || _atlas == atlas;

    // Pack one block's glyphs into the pending segment: each glyph's LOCAL rect folded by the node-RELATIVE scale/translate
    // (the axis-aligned rect can hold that), its transform SLOT, its atlas UV, and the block's foreground as a per-instance
    // colour. NO world matrix is applied here - the glyph VS applies the node matrix (from the transform table at the slot)
    // on the GPU. False (no write) for a rotated/sheared RELATIVE transform (the axis-aligned rect can't hold it) or a
    // buffer overflow this frame -> the caller renders that block via the per-block direct draw. Mirrors RectBatchCollector.
    public bool TryAdd(TextRenderComponent tc, Matrix4x4F relWorld, int transformSlot, Rect2D scissor, FontAtlas atlas, Rect logicalBounds)
    {
        var n = tc.GlyphRun.Count;

        EnsureCpuCapacity(Count + n);
        if (Count + n > GpuCapacity) return false;   // won't fit this frame's GPU buffer -> direct
        if (!Pack(tc, relWorld, transformSlot, Count)) return false;

        Count += n;
        _atlas = atlas;
        _fontRenderer = tc.FontRenderer;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Re-bake an already-flushed block into the run of slots it ALREADY occupies - <see cref="UpdateSlot"/> for
    /// a unit that owns several slots. The recorded segment still spans this run, so the frame can be replayed instead of
    /// re-walked: a counter whose glyph count holds steady costs one range upload, not a walk of the scene. The caller
    /// checks the count and atlas still match (RenderCache.IsSlotPatchable); false here means the block no longer packs
    /// at all (a rotated relative transform) and the walk must take it.</summary>
    public bool UpdateRun(IGraphicsDevice device, int first, TextRenderComponent tc, Matrix4x4F relWorld, int transformSlot)
    {
        PrepareRetainedWrite(device);
        if (!Pack(tc, relWorld, transformSlot, first)) return false;
        UploadRange(first, tc.GlyphRun.Count);
        return true;
    }

    // Write one block's glyphs at [at, at+run.Count): each glyph's LOCAL rect folded by the node-RELATIVE scale/translate
    // (the axis-aligned rect can hold that), its transform SLOT, its atlas UV, and the block's foreground as a per-instance
    // colour. NO world matrix is applied here - the glyph VS applies the node matrix (from the transform table at the slot)
    // on the GPU. False (no write) for a rotated/sheared RELATIVE transform. Mirrors RectBatchCollector's bake.
    private bool Pack(TextRenderComponent tc, Matrix4x4F relWorld, int transformSlot, int at)
        => PackInto(tc, relWorld, transformSlot, Items.AsSpan(at, tc.GlyphRun.Count));

    /// <summary>Bake one block's glyphs into <paramref name="dst"/>. Where they land is the caller's business - the
    /// retained arena during a walk, the patch stage during a repair - and the bake is the same either way.</summary>
    private static bool PackInto(TextRenderComponent tc, Matrix4x4F relWorld, int transformSlot, Span<GlyphItem> dst)
    {
        const float eps = 1e-4f;
        if (Math.Abs(relWorld.M12) > eps || Math.Abs(relWorld.M21) > eps) return false;

        var run = tc.GlyphRun;                        // FROZEN snapshot - the applier never reads the live TextLayout here
        var area = tc.RenderingParameters.TextArea;
        var color = ((SolidColorBrush)tc.Foreground).Color.ToVector4();
        color.W *= (float)tc.RenderData.Opacity;      // fold the element's opacity into the glyph alpha

        float sx = relWorld.M11, sy = relWorld.M22, tx = relWorld.M41, ty = relWorld.M42;
        float ax = (float)area.X, ay = (float)area.Y;
        var glyphs = run.Glyphs;
        for (var i = 0; i < run.Count; i++)
        {
            var d = glyphs[i].ArrangeRect;   // local x, y, w, h
            dst[i] = new GlyphItem
            {
                LocalRect = new Vector4F((d.X + ax) * sx + tx, (d.Y + ay) * sy + ty, d.Z * sx, d.W * sy),
                Source = glyphs[i].Source,
                Params = new Vector4F(transformSlot, glyphs[i].Layer, glyphs[i].Depth, 0),
                Color = color
            };
        }

        return true;
    }

    protected override void DrawSegment(IGraphicsDevice device, Buffer<GlyphItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        var stride = (ulong)Marshal.SizeOf<GlyphItem>();
        _fontRenderer.DrawBatch(device.SamplerStates.LinearFont, _atlas,
            buffer.GetDeviceAddress() + firstInstance * stride, TransformsAddress, count, projection);
    }
}
