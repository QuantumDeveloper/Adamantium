// ---- ROUNDED CLIPPING -------------------------------------------------------------------------------------------
// A scissor is a RECTANGLE, so a rounded container cut the corners off its content squarely - a card's shimmer reached
// the corner and the corner went sharp. The shape travels in a transform-table slot instead (TransformTable.SetClip):
// row 0 of the slot's matrix is the clip rectangle in DEVICE pixels, row 1 its four radii, Params.x marks it as carrying
// one. Device pixels because that is the space a fragment's own position is already in - no matrix, no interpolation.
//
// COVERAGE, not discard. Discarding gives a stair-stepped corner exactly where the point was to look tidy, and throws
// away the early depth test; the distance field is what every shape here computes anyway, so the clip is one more of
// them and its edge is anti-aliased like any other.
//
// The table is read in the VERTEX stage and the SHAPE travels to the fragment stage as varyings. The shape is
// per-INSTANCE, so one fetch a vertex replaces one a fragment - and BrushEffect already reaches for the node table from
// the vertex stage for the same kind of value ("reaching the node table from the PIXEL stage blanks the window on this
// driver", on the gradient's Fade), so this is the established shape of the answer here rather than a new one.
//
// It also walks away from something not understood. Reading the table in the ELLIPSE pass's fragment stage lost the
// device (2 starts of 2) at slot 46, while slot 0 and not reading at all were both fine. Two explanations were then
// DISPROVED by measurement, and neither should be repeated: the reflected $Globals layout is identical in both stages
// (same buffer, same size, TransformsAddress at the same offset), and both stages read the SAME address (each stage's
// value painted into its own colour channel and compared per pixel - they matched). The failure does not reproduce on
// this design (4 starts of 4, no loss), so the cause is unknown and nothing in the framework is known to be broken.
//
// Its own header rather than a section of ShapeMath: the TEXT batch clips by the same shape through the same slot and is
// compiled as a separate effect, so this is exactly the piece the two have to share - one field, not two that drift.
// Both write #include "Includes/ClipMath.fxh"; Adamantium.FX links this very file in rather than keeping a copy.
//
// It deliberately declares no globals of its own - it uses NodeSlot and TransformsAddress, which every effect that
// includes it already declares, so it adds nothing to a parameter block that has been shown to be at its limit.

// The corner this fragment belongs to, out of the four (x = TL, y = TR, z = BR, w = BL - the CPU CornerRadius order).
// SDF space has y DOWN (the quad's corner 0 is the TOP-left), so a negative Local.y is the top half. Every rounded-rect
// helper picks its radius through this one function, which is what keeps the four corners INDEPENDENT: the field stays
// continuous across the axes because the +r/-r of the offset cancels on a straight edge, so neighbouring corners never
// have to agree.
float CornerRadiusAt(float2 p, float4 radii)
{
    return p.x < 0.0 ? (p.y < 0.0 ? radii.x : radii.w)
                     : (p.y < 0.0 ? radii.y : radii.z);
}

// Signed distance (device px) to a rounded rect with a selectable outer-corner JOIN: 0 = miter (sharp, Chebyshev outer
// corner), 1 = bevel (45-deg chamfer), 2 = round (Euclidean, the natural offset). Straight edges are identical across all
// three; only the corner (both q>0) differs. This is what gives stroke-join parity with the pen (PenLineJoin).
float SdRoundRectJoin(float2 p, float2 b, float4 radii, int joinType)
{
    float r = CornerRadiusAt(p, radii);
    float2 q = abs(p) - b + r;
    float inside = min(max(q.x, q.y), 0.0);
    float2 qp = max(q, float2(0.0, 0.0));
    // A ROUNDED geometry (r>0) already curves the corner - the join is moot and applying miter/bevel would (wrongly)
    // reshape the FILL, so only a (near-)sharp corner honours the join. Round join is always the plain Euclidean offset.
    float outside = (r > 0.5 || joinType == 2) ? length(qp)
                  : (joinType == 1) ? (qp.x + qp.y)                   // bevel: L1 - a 45-deg chamfer at the corner, but a
                                                                     // straight edge (one component 0) stays exact
                  : max(qp.x, qp.y);                                  // miter (sharp)
    return inside + outside - r;
}

// Box: xy = the clip rect's origin in DEVICE pixels, zw = its size. zw = 0 means THERE IS NO CLIP.
float4 ClipShapeBox(float slotIndex)
{
    if (slotIndex < 0.0) return float4(0.0, 0.0, 0.0, 0.0);

    // The address is taken HERE rather than passed in: a pointer-to-struct parameter is what this driver's shader
    // compiler falls over on, and every working shader in this tree casts the address locally.
    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    NodeSlot clip = nodes[(uint)slotIndex];
    if (clip.Params.x < 0.5) return float4(0.0, 0.0, 0.0, 0.0);

    return clip.World[0];
}

/// The four corner radii of that same clip, in device pixels.
float4 ClipShapeRadii(float slotIndex)
{
    if (slotIndex < 0.0) return float4(0.0, 0.0, 0.0, 0.0);

    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    NodeSlot clip = nodes[(uint)slotIndex];
    if (clip.Params.x < 0.5) return float4(0.0, 0.0, 0.0, 0.0);

    return clip.World[1];
}

// The whole thing in ONE table read, for a pass that cannot do the fetch in its vertex stage. TEXT is that pass: the
// glyph vertex shader already reads the table for its matrix, and this driver AVs inside vkCreateShadersEXT on a SECOND
// read from that shader - measured again here, 4 starts of 4, exactly as the note on GlyphItem.Params said. Its PIXEL
// shader reads the table nowhere, so there the fetch is the first one and compiles.
//
// One read, not the two ClipShapeBox + ClipShapeRadii would make: the same reason the pair exists at all is that the
// vertex stage can afford them per instance, and a fragment stage cannot.
float ClipCoverageBySlot(float2 fragment, float slotIndex)
{
    if (slotIndex < 0.0) return 1.0;

    NodeSlot* nodes = (NodeSlot*)TransformsAddress;
    NodeSlot clip = nodes[(uint)slotIndex];
    if (clip.Params.x < 0.5) return 1.0;

    float2 halfSize = max(clip.World[0].zw * 0.5, float2(1.0, 1.0));
    float2 local = fragment - (clip.World[0].xy + halfSize);
    float lim = min(halfSize.x, halfSize.y);
    float4 r4 = min(clip.World[1], float4(lim, lim, lim, lim));

    float d = SdRoundRectJoin(local, halfSize, r4, 2);
    float aa = fwidth(d) + 1e-4;
    return 1.0 - smoothstep(-aa, aa, d);
}

// The fragment's coverage under that shape - no buffer access, so any pass can call it.
float ClipCoverage(float2 fragment, float4 box, float4 radii)
{
    if (box.z <= 0.0) return 1.0;   // no clip

    float2 halfSize = max(box.zw * 0.5, float2(1.0, 1.0));
    float2 local = fragment - (box.xy + halfSize);
    float lim = min(halfSize.x, halfSize.y);
    float4 r4 = min(radii, float4(lim, lim, lim, lim));

    float d = SdRoundRectJoin(local, halfSize, r4, 2);
    float aa = fwidth(d) + 1e-4;
    return 1.0 - smoothstep(-aa, aa, d);
}
