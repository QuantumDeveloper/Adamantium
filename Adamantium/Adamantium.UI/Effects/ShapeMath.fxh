// SHAPE MATHS - the signed-distance fields every instanced fill is cut from, and the measurements that go with them:
// how many device pixels one unit of a slot spans, the arc length along a contour, the curvature at a point, and the
// expansion of the analytic-AA ring.
//
// Shared by BOTH effects deliberately: a gradient rect is the SAME rounded rect as a solid one, only filled
// differently. Include AFTER CommonData.fxh (it reads Projection/ViewportSize and the fringe layouts) and BEFORE
// StrokeMath.fxh, which builds on the corner radius and the joins.

// ---- Shared SDF fill+stroke compositing --------------------------------------------------------------------------
// Both SDF families (rounded-rect, ellipse) share the same stroke story: given the signed distance `d` to the contour
// (device-px, negative inside), a fill and an OPTIONAL stroke are composited in ONE pass. The stroke is a ring built
// analytically from the same `d` (no geometry): a solid stroke is `abs(d - align*halfW) - halfW`, and dashes/trim
// modulate it via `strokeMask` (0..1, computed by the caller from arc length). Output is STRAIGHT alpha (drawn with a
// straight AlphaBlend, like the solid fills) - so two straight layers (fill under stroke) are composited to one.
//
// stroke0 = (width_px, align[-1 inside/0 center/+1 outside], dashOn, dashGap); stroke1 = (dashOffset, trimStart, trimEnd, flags).
// How many DEVICE PIXELS one unit of a slot's space spans, per axis. The SDF shapes are baked in the space of their
// transform-table slot, and that space is not the screen's: an element whose own scale lives in the slot (anything that
// drives its own transform - an animated one is exactly that) reaches the shader with, say, a 1-unit-wide rect and a
// scale of 125 in the matrix. Distances then mean 125 px along X and 1 px along Y, and ONE scalar AA width cannot serve
// both - which is what smeared the tab-selection bar along its whole length. Callers convert their SDF inputs with this,
// so `d` comes out in pixels and fwidth(d) is ~1 on every axis.
// NB single exit, no early return: an early `return` in a .fx body has repeatedly tripped an AV inside NVVM here.
float2 SlotPixelScale(float4x4 nodeWorld)
{
    float4x4 m = mul(nodeWorld, Projection);
    float2 halfVp = ViewportSize * 0.5;
    float2 ax = mul(float4(1.0, 0.0, 0.0, 0.0), m).xy * halfVp;
    float2 ay = mul(float4(0.0, 1.0, 0.0, 0.0), m).xy * halfVp;
    float2 scale = max(float2(length(ax), length(ay)), float2(1e-4, 1e-4));
    return ViewportSize.x < 1.0 ? float2(1.0, 1.0) : scale;   // no viewport supplied: leave the bake untouched
}

// The corner this fragment belongs to, out of the four (x = TL, y = TR, z = BR, w = BL - the CPU CornerRadius order).
// SDF space has y DOWN (the quad's corner 0 is the TOP-left), so a negative Local.y is the top half. Every rounded-rect
// helper below picks its radius through this one function, which is what keeps the four corners INDEPENDENT: the field
// stays continuous across the axes because the +r/-r of the offset cancels on a straight edge, so neighbouring corners
// never have to agree.
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

// Approximate SIGNED DISTANCE (device px) to an ellipse boundary: the implicit F = length(p/half) - 1 normalised by the
// length of its gradient (first-order/Taylor distance). Exact for a circle (rx==ry); for rx!=ry it's the correct shape
// with sub-pixel-accurate distance near the boundary - which is exactly where fill AA and the stroke ring live.
float SdEllipse(float2 p, float2 half)
{
    float2 h = max(half, float2(1e-6, 1e-6));
    float2 nq = p / h;
    float L = max(length(nq), 1e-6);
    float2 grad = float2(nq.x / h.x, nq.y / h.y) / L;   // d(F)/d(p)
    return (L - 1.0) / max(length(grad), 1e-6);
}

// A REGULAR POLYGON, in the same family as the ellipse and for the same reason: it is one field, and the only thing that
// separates a triangle from a circle is how many corners you ask for (large N is a circle to the pixel). Exact, so it
// self-anti-aliases and strokes like everything else here.
//
// `p` and `half` are the same as the ellipse's, and the shape is INSCRIBED in that box: the maths runs in normalised
// space (circumradius 1) and the distance is scaled back by the smaller half-axis - the same first-order treatment of an
// anisotropic box that SdEllipse gives, so a squashed polygon behaves like a squashed circle.
//
// The first vertex sits at angle 0, on the +x axis, because that is where the tessellator puts it (Shapes.Polygon walks
// 2*pi*i/N from there) - a polygon that batches must be the same polygon that the fallback tessellates, rotation
// included.
float SdRegularPolygon(float2 p, float2 half, float n, float startAngle)
{
    float2 h = max(half, float2(1e-6, 1e-6));
    float2 q = p / h;                       // normalised: the shape is the unit circumradius polygon

    // Turn the shape by rolling the SAMPLE the other way - and do it HERE, in normalised space, where the corners sit on
    // a unit circle. Rotating the fragment before the divide would rotate the box too, so a squashed hexagon would swing
    // out of the slot it is inscribed in; rotating after it moves the corners along the ellipse the box inscribes, which
    // is exactly what Shapes.Polygon does with the same angle (radii * cos/sin of start + 2*pi*i/N).
    float ca = cos(startAngle);
    float sa = sin(startAngle);
    q = float2(q.x * ca + q.y * sa, q.y * ca - q.x * sa);

    float an = 3.14159265 / n;              // half of one sector

    // Fold into a single half-sector, measured from the +x axis so that vertex 0 lands on it. What is left is a point
    // whose x runs along the apothem and whose y is its (positive) offset along the edge.
    float a = atan2(q.y, q.x);
    float wrapped = a - 2.0 * an * floor(a / (2.0 * an) + 0.5);   // into [-an, an], centred on a VERTEX
    float2 folded = length(q) * float2(cos(wrapped), abs(sin(wrapped)));

    // Distance to the edge running from that vertex to the next: a segment, so a point past the vertex measures to the
    // vertex itself rather than to the edge's infinite line.
    float2 v0 = float2(1.0, 0.0);
    float2 v1 = float2(cos(2.0 * an), sin(2.0 * an));
    float2 e = v1 - v0;
    float2 w = folded - v0;
    float2 d = w - e * saturate(dot(w, e) / max(dot(e, e), 1e-9));

    // Inside is the side the centre is on. cross(e, w) changes sign exactly across the edge's line.
    float side = (e.x * w.y - e.y * w.x) > 0.0 ? -1.0 : 1.0;
    return length(d) * side * min(h.x, h.y);
}

// Arc-length `s` (device px) of the point on the ROUNDED-RECT contour nearest `p`, and the perimeter. Exact/closed-form.
// Traversal CCW from the start of the top-right arc: TR arc, top edge, TL arc, left edge, BL arc, bottom edge, BR arc,
// right edge. (Start point is arbitrary for dashes; dashOffset shifts the phase.)
float RoundRectArc(float2 p, float2 b, float4 radii, out float perimeter)
{
    // Dashes are measured on the CENTRELINE. In the corner, s = corner-start + phi*r uses the fragment's ANGLE (phi),
    // not its radius, so it's already uniform across the stroke width AND continuous with the edges - the dash flows
    // around the corner exactly like on a straight edge (and like the ellipse). No per-corner "single state" is needed.
    // With four INDEPENDENT radii every arc and every edge has its own length, so the traversal is accumulated corner by
    // corner instead of multiplying one quarter-arc by four. Order (CCW in SDF space, y down): BR arc, bottom edge,
    // BL arc, left edge, TL arc, top edge, TR arc, right edge.
    float rTL = radii.x, rTR = radii.y, rBR = radii.z, rBL = radii.w;
    const float HALF_PI = 1.5707963268;
    float aBR = HALF_PI * rBR, aBL = HALF_PI * rBL, aTL = HALF_PI * rTL, aTR = HALF_PI * rTR;
    float eBottom = 2.0 * b.x - rBR - rBL;
    float eLeft   = 2.0 * b.y - rBL - rTL;
    float eTop    = 2.0 * b.x - rTL - rTR;
    float eRight  = 2.0 * b.y - rTR - rBR;
    perimeter = aBR + eBottom + aBL + eLeft + aTL + eTop + aTR + eRight;

    float sBottom = aBR;                      // where each segment STARTS along the traversal
    float sBL     = sBottom + eBottom;
    float sLeft   = sBL + aBL;
    float sTL     = sLeft + eLeft;
    float sTop    = sTL + aTL;
    float sTR     = sTop + eTop;
    float sRight  = sTR + aTR;

    float r = CornerRadiusAt(p, radii);
    float bx = b.x - r, by = b.y - r;
    float ax = abs(p.x), ay = abs(p.y);
    float cx = ax - bx, cy = ay - by;

    if (cx > 0.0 && cy > 0.0)                                   // corner -> arc-length by angle (phi), uniform + continuous
    {
        float phi = atan2(cy, cx);
        float s;
        if      (p.x >= 0.0 && p.y >= 0.0) s = phi * rBR;
        else if (p.x <  0.0 && p.y >= 0.0) s = sBL + (HALF_PI - phi) * rBL;
        else if (p.x <  0.0 && p.y <  0.0) s = sTL + phi * rTL;
        else                               s = sTR + (HALF_PI - phi) * rTR;
        return s;
    }
    // Classify by the NEAREST edge, not just cx/cy vs the CORNER radius r: a thick stroke reaches MORE than r inside, so
    // the inner part of a top/bottom-edge stroke has cy<=0 (inside the inner box) yet is still nearest the horizontal
    // edge. Deciding by cx/cy alone mis-routed that inner sliver (width halfW-r) to the VERTICAL-edge arc-length -> a thin
    // mis-dashed line on the inner edge that only showed when halfW > r (thick stroke / small corner).
    // An edge is anchored at the corner it STARTS from, which is not necessarily the corner nearest this fragment.
    bool horizontal = (cx <= 0.0) && (cy > 0.0 || (b.y - ay) < (b.x - ax));
    float s;
    if (horizontal) s = (p.y >= 0.0) ? sBottom + ((b.x - rBR) - p.x) : sTop + (p.x + (b.x - rTL));
    else            s = (p.x >= 0.0) ? sRight + (p.y + (b.y - rTR)) : sLeft + ((b.y - rBL) - p.y);
    return s;
}

// Arc-length `s` (device px) along an ELLIPSE contour to the fragment's radial projection, and the perimeter. Perimeter
// is Ramanujan's closed form; the partial length is a short trapezoidal integral of ds/dt = sqrt(rx^2 sin^2 t + ry^2
// cos^2 t) - exact for a circle, sub-pixel for real ellipses. The loop runs only for the thin stroke ring's fragments.
float EllipseArc(float2 p, float2 h, out float perimeter)
{
    float a = max(h.x, h.y), b = min(h.x, h.y);
    float hh = ((a - b) * (a - b)) / max((a + b) * (a + b), 1e-6);
    perimeter = 3.14159265 * (a + b) * (1.0 + 3.0 * hh / (10.0 + sqrt(4.0 - 3.0 * hh)));

    float t = atan2(p.y * h.x, p.x * h.y);        // parametric angle of the radial projection
    if (t < 0.0) t += 6.28318530718;

    const int N = 16;
    float dt = t / float(N);
    float s = 0.0;
    float prev = h.y;                             // ds/dt at t=0 = ry
    for (int i = 1; i <= N; i++)
    {
        float u = dt * float(i);
        float su = sin(u), cu = cos(u);
        float cur = sqrt(h.x * h.x * su * su + h.y * h.y * cu * cu);
        s += 0.5 * (prev + cur) * dt;
        prev = cur;
    }
    return s;
}

// Same for a rounded rect: the corner arcs bend with the corner radius, the four edges are straight.
float RoundRectCurvRadius(float2 p, float2 b, float4 radii)
{
    float r = CornerRadiusAt(p, radii);
    float2 q = abs(p) - (b - r);
    return (q.x > 0.0 && q.y > 0.0 && r > 0.5) ? r : 1e9;
}

// Radius of curvature of the ellipse at the fragment's parametric angle: |r'|^3 / (rx*ry) for x = rx cos t, y = ry sin t.
// Kept as its own single-expression helper - the arc functions above have early returns, and giving one of THOSE a second
// out-parameter made the NVIDIA NVVM compiler AV in vkCreateShadersEXT (see the note on DashTrimMaskCapped).
float EllipseCurvRadius(float2 p, float2 h)
{
    float t = atan2(p.y * h.x, p.x * h.y);
    float st = sin(t), ct = cos(t);
    float e = sqrt(h.x * h.x * st * st + h.y * h.y * ct * ct);
    return (e * e * e) / max(h.x * h.y, 1e-6);
}

// The ring's vertex, expanded. Shared by every fringe pass (solid / pattern / gradient): they differ only in WHICH
// record supplies the matrix and the colour, and the expansion itself must stay one definition - it is the thing that
// makes the ring exactly one device pixel wide. `coverage` comes out 1 on the contour and 0 on the outer edge.
float4 ExpandFringe(FringeVertex v, float4x4 m, out float coverage)
{
    float4 clip = mul(float4(v.Position, 0.0, 1.0), m);
    float outer = dot(v.Dir0, v.Dir0) + dot(v.Dir1, v.Dir1);
    if (outer > 0.0)
    {
        // Edge directions -> PIXEL space (w = 0 drops the translation), so the miter is perpendicular to the edge as the
        // rasterizer sees it - correct under anisotropic scale, skew and rotation alike.
        float2 halfVp = max(ViewportSize, float2(1.0, 1.0)) * 0.5;
        float w = max(clip.w, 1e-6);
        float2 e0 = mul(float4(v.Dir0, 0.0, 0.0), m).xy / w * halfVp;
        float2 e1 = mul(float4(v.Dir1, 0.0, 0.0), m).xy / w * halfVp;
        e0 = length(e0) > 1e-9 ? normalize(e0) : float2(0.0, 0.0);
        e1 = length(e1) > 1e-9 ? normalize(e1) : float2(0.0, 0.0);
        float2 n0 = float2(-e0.y, e0.x);
        float2 n1 = float2(-e1.y, e1.x);
        float2 sum = n0 + n1;
        float2 miter = length(sum) > 1e-4 ? normalize(sum) : n0;   // a 180-degree reversal has no bisector: use one normal
        float denom = max(dot(miter, n0), 0.25);                   // clamp the corner spike to <= 4x
        clip.xy += miter * (FringePixels / denom) / halfVp * w;
    }
    coverage = outer > 0.0 ? 0.0 : 1.0;
    return clip;
}

