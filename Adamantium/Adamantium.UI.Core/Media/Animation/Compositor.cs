using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// The animations the RENDER thread plays by itself. Not a thread of its own - work moved onto the render thread, which
/// already runs its own loop and presents at its own pace, and is the only consumer of these values anyway.
/// </summary>
/// <remarks>
/// The point is what happens when the loop thread STALLS - a theme cascade re-templating the tree, a heavy layout. Today every
/// animation advances inside <c>SetValue</c>, so the loop stalling freezes every spinner on screen: precisely when the user
/// most needs to see one. A composited animation instead lives here, where the loop thread cannot hold it up.
///
/// **One clock.** The render thread owns <c>Elapsed</c>; the loop thread READS it and mirrors the value into the property
/// system so hit-testing and bindings still agree with the screen (raw UWP Composition skips this, which is why a visual
/// animating on its compositor hit-tests where it would have stood still). Two clocks would let the two drift, so there is
/// exactly one, and it is the one that draws.
///
/// **What the render thread may touch.** Nothing shared and mutable. It reads an immutable <see cref="Basis"/> published by
/// the loop thread (reference swap), overrides the members its curve animates, and composes the matrix through the very same
/// <see cref="TransformValues.ToMatrix"/> the live Transform uses. It never reads a property, so there is nothing to race.
///
/// **Why the element must be a motion node.** A world-baked element cannot be moved without re-recording its instances - and
/// re-recording is the loop thread's job. A motion node's instances reference its transform-table slot, so moving it is ONE
/// 64-byte matrix write, which is exactly what the render thread can do alone. Taking a transform over therefore promotes its
/// element (see <see cref="TryTakeOver"/>).
///
/// **Two channels.** TRANSFORM (a Transform element) writes one motion-node matrix and IS mirrored to the property system, so
/// hit-testing agrees with the screen. PAINT (a brush's own Opacity/colour) is NOT mirrored - colour touches neither layout
/// nor hit-test - so the loop thread stops advancing it entirely; the render thread republishes the brush's snapshot and the
/// render cache re-bakes the slots that read it. One shared brush drives many elements (the loading-skeleton pulse animates
/// ONE brush every card paints with), which is exactly the case that cost the most on the loop thread before.
/// </remarks>
public static class Compositor
{
    /// <summary>Everything the matrix needs that the curve does NOT animate - the element's base transform values, its layout
    /// position, and the size the render-transform origin resolves against. Immutable, published by the loop thread whenever
    /// layout changes; the render thread only ever reads the reference.
    ///
    /// While the loop thread is stalled this cannot go stale, because nothing can change it: no loop, no layout.</summary>
    public sealed class Basis
    {
        public Basis(TransformValues values, Vector2 boundsLocation, Vector2 origin, Size renderSize)
        {
            Values = values;
            BoundsLocation = boundsLocation;
            Origin = origin;
            RenderSize = renderSize;
        }

        public TransformValues Values { get; }
        public Vector2 BoundsLocation { get; }
        public Vector2 Origin { get; }
        public Size RenderSize { get; }
    }

    /// <summary>One animation the render thread plays - a Transform (one element, one matrix) or a Paint brush (one shared
    /// brush, many elements). The two share a clock and a curve; how the value is APPLIED differs, so <see cref="Channel"/>
    /// selects between the transform-only and paint-only state.</summary>
    public sealed class Entry
    {
        // Transform: one element moved by one matrix write.
        private Entry(Transform target, IUIComponent owner, AnimationCurve curve, Basis basis, double resumeElapsed)
        {
            Target = target;
            Channel = CompositorChannel.Transform;
            Owner = owner;
            Curve = curve;
            _basis = basis;
            _startTimestamp = Stopwatch.GetTimestamp() - (long)(resumeElapsed * Stopwatch.Frequency);
            Local = ComposeLocal(basis, curve, Elapsed);
        }

        // Paint: one shared brush whose animated snapshot the render thread republishes each present.
        private Entry(Brush target, AnimationCurve curve, Brush paintBase, double resumeElapsed)
        {
            Target = target;
            Channel = CompositorChannel.Paint;
            Curve = curve;
            _paintBase = paintBase;
            _startTimestamp = Stopwatch.GetTimestamp() - (long)(resumeElapsed * Stopwatch.Frequency);
        }

        // Opacity: one element whose alpha the render thread writes into its opacity slot. No basis and no snapshot - the
        // curve's value IS the applied state, and everything under the element composes it at draw time.
        private Entry(IUIComponent owner, AnimationCurve curve, double resumeElapsed)
        {
            Target = (AdamantiumComponent)owner;
            Channel = CompositorChannel.Opacity;
            Owner = owner;
            Curve = curve;
            _startTimestamp = Stopwatch.GetTimestamp() - (long)(resumeElapsed * Stopwatch.Frequency);
            Alpha = (float)curve.Evaluate(curve.Tracks[0], Elapsed);
        }

        // resumeElapsed carries a re-templated animation's phase across the target swap, so a spinner picks up where the old
        // one was instead of snapping to the start (the theme-swap stutter). 0 for a fresh start.
        internal static Entry ForTransform(Transform t, IUIComponent owner, AnimationCurve curve, Basis basis, double resumeElapsed = 0) => new(t, owner, curve, basis, resumeElapsed);
        internal static Entry ForPaint(Brush b, AnimationCurve curve, Brush paintBase, double resumeElapsed = 0) => new(b, curve, paintBase, resumeElapsed);
        internal static Entry ForOpacity(IUIComponent owner, AnimationCurve curve, double resumeElapsed = 0) => new(owner, curve, resumeElapsed);

        /// <summary>The element's alpha at the last <see cref="Recompose"/> - what the render thread writes to its slot.</summary>
        public float Alpha { get; private set; }

        public AdamantiumComponent Target { get; }
        public CompositorChannel Channel { get; }
        public AnimationCurve Curve { get; }

        // Transform-only.
        public IUIComponent Owner { get; }

        /// <summary>True when the compositor itself flipped Owner into a motion node for THIS animation (it was a plain
        /// world-baked element). Release then flips it back - a node that was ALREADY a motion node (FlipTile, a scroll
        /// viewport) has this false and is left promoted. Carried across a re-animation of the same target.</summary>
        internal bool Promoted { get; set; }

        private volatile Basis _basis;
        public Matrix4x4F Local { get; private set; }

        // Paint-only: the brush's own (un-animated) values, refreshed on the loop thread. The render thread builds each
        // present's snapshot from it and never reads the live brush.
        private volatile Brush _paintBase;

        // Paint-only: a quantized fingerprint of the last published values, so a present whose baked bytes would be identical
        // rebuilds and re-bakes NOTHING. The render thread presents far faster than an animation changes a pixel (a 1.2s
        // opacity pulse crosses only tens of alpha bytes), so without this a shared brush would rebuild its snapshot and
        // re-bake all its slots ~1000x/second to no visible effect. The quantum is per-property (see Brush.PaintQuantum).
        private long _lastPaintTag = long.MinValue;

        /// <summary>Did the LAST <see cref="Recompose"/> actually change the paint (so the render cache must re-bake the
        /// brush's slots)? Meaningful only for a Paint entry.</summary>
        public bool PaintChanged { get; private set; }

        private readonly long _startTimestamp;

        /// <summary>Seconds since this animation began - read straight off the monotonic clock, not accumulated. Both threads
        /// therefore get the same answer for the same instant, no matter who asks, how often, or in what order: there is
        /// nothing to keep in step because there is no counter. (Accumulating a delta would need every reader to agree on who
        /// advances it - and would drift the moment two did.)</summary>
        public double Elapsed => (Stopwatch.GetTimestamp() - _startTimestamp) / (double)Stopwatch.Frequency;

        // When the render thread last APPLIED this transform (drew its animated matrix). The loop's mirror suppression keys
        // off this: if the render thread is applying (recently), the loop must NOT also re-bake it (double work). But if it is
        // NOT applying - the owner isn't recorded yet, e.g. a spinner just re-templated by a theme swap - the picture is
        // frozen, so the loop must step in and force a re-record. Written by the render thread, read by the loop; a torn long
        // read at worst mis-judges the window by one and is corrected next frame, so no interlock is needed.
        private long _lastAppliedTs;
        internal void MarkApplied() => _lastAppliedTs = Stopwatch.GetTimestamp();
        public bool AppliedRecently => _lastAppliedTs != 0 && Stopwatch.GetTimestamp() - _lastAppliedTs < Stopwatch.Frequency / 20;   // ~50 ms

        public Basis CurrentBasis
        {
            get => _basis;
            internal set => _basis = value;
        }

        internal Brush PaintBase
        {
            get => _paintBase;
            set { _paintBase = value; _lastPaintTag = long.MinValue; }   // a new base -> force one republish so a recolour shows
        }

        /// <summary>Recompute this animation's applied state for RIGHT NOW. Idempotent - calling it twice in a frame is
        /// harmless. Transform: recompose the matrix into <see cref="Local"/>. Paint: build and publish the brush snapshot
        /// (the render cache then re-bakes the slots that read it).</summary>
        internal void Recompose()
        {
            if (Channel == CompositorChannel.Transform)
            {
                Local = ComposeLocal(_basis, Curve, Elapsed);
                return;
            }

            if (Channel == CompositorChannel.Opacity)
            {
                Alpha = (float)Curve.Evaluate(Curve.Tracks[0], Elapsed);
                return;
            }

            // Paint: only rebuild + republish when the quantized values actually moved (see _lastPaintTag).
            var brush = (Brush)Target;
            var elapsed = Elapsed;
            var tag = PaintTag(brush, Curve, elapsed);
            PaintChanged = tag != _lastPaintTag;
            if (!PaintChanged) return;
            _lastPaintTag = tag;
            brush.PublishSnapshot(_paintBase.BuildAnimatedSnapshot(Curve, elapsed));
        }

        // A fingerprint of the curve's values at this instant, each quantized to ITS property's visible resolution (see
        // Brush.PaintQuantum) - two instants with the same fingerprint bake byte-identical, so the second needs no work.
        // Per-property because opacity's exact quantum (8-bit) would visibly step a geometric one, and the fine geometric
        // quantum would re-bake a shared opacity brush far more than its 8-bit output can show. Order-dependent combine.
        private static long PaintTag(Brush brush, AnimationCurve curve, double elapsed)
        {
            long tag = 17;
            foreach (var track in curve.Tracks)
                tag = tag * 31 + (long)(curve.Evaluate(track, elapsed) * brush.PaintQuantum(track.Property));
            return tag;
        }

        // The same composition UIComponent.LocalTransform does - the render transform applied around the origin, then the
        // layout offset - but built from captured data instead of live properties.
        private static Matrix4x4F ComposeLocal(Basis basis, AnimationCurve curve, double elapsed)
        {
            var values = basis.Values;
            foreach (var track in curve.Tracks)
                values.Set(track.Property, curve.Evaluate(track, elapsed));

            var matrix = (Matrix4x4F)values.ToMatrix();

            var origin = basis.Origin;
            if (origin.X != 0 || origin.Y != 0)
            {
                var ox = (float)(origin.X * basis.RenderSize.Width);
                var oy = (float)(origin.Y * basis.RenderSize.Height);
                matrix = Matrix4x4F.Translation(-ox, -oy, 0) * matrix * Matrix4x4F.Translation(ox, oy, 0);
            }

            return matrix * Matrix4x4F.Translation((float)basis.BoundsLocation.X, (float)basis.BoundsLocation.Y, 0);
        }
    }

    // Guarded by itself: the loop thread adds/removes, the render thread iterates. Both are rare-to-modest operations (an
    // animation starting is not a per-frame event), so a plain lock is right - a lock-free structure here would buy nothing
    // and cost the ability to reason about it.
    private static readonly object Gate = new();
    private static readonly List<Entry> Entries = new();

    /// <summary>Hand an animation to the render thread, if it can play it. Loop thread, when the animation starts. Returns
    /// null when it cannot - the caller then keeps the animation on the loop thread, as before.</summary>
    public static Entry TryTakeOver(AdamantiumComponent target, AnimationCurve curve, double resumeElapsed = 0)
    {
        var channel = AnimationChannels.Of(target, curve);
        Entry entry;
        switch (channel)
        {
            case CompositorChannel.Transform:
            {
                if (target is not Transform transform || transform.Owner is not { } owner) return null;

                // A world-baked element cannot be moved by a matrix write. Promote it - and mark it structural, because its
                // subtree's instances must be re-baked in the node's space before the slot means anything. Remember WE did
                // it (Promoted) so Release un-promotes it; a node that was already a motion node is left as it was.
                var promoted = !owner.IsRenderMotionNode;
                if (promoted)
                {
                    owner.IsRenderMotionNode = true;
                    RenderDirty.MarkStructural(owner);
                }
                entry = Entry.ForTransform(transform, owner, curve, CaptureBasis(transform, owner), resumeElapsed);
                entry.Promoted = promoted;
                break;
            }
            case CompositorChannel.Paint:
            {
                // A shared brush whose colour/opacity/geometry animates. No element to promote and no matrix: the render
                // thread republishes the brush's snapshot each present, and the slots painting with it are re-baked from it.
                // Any double paint property works - BuildAnimatedSnapshot applies it generically, and AffectsPaint (which the
                // channel already required) guarantees a re-bake suffices.
                if (target is not Brush brush) return null;
                entry = Entry.ForPaint(brush, curve, brush.CaptureBase(), resumeElapsed);
                break;
            }
            case CompositorChannel.Opacity:
            {
                // The element's own alpha. Nothing to promote and nothing to capture: the value goes into the element's
                // opacity slot, and every instance beneath it already carries that slot's index - which is what makes
                // this the twin of Transform, one write for a whole subtree.
                if (target is not IUIComponent owner) return null;
                entry = Entry.ForOpacity(owner, curve, resumeElapsed);
                break;
            }
            default:
                return null;
        }

        lock (Gate)
        {
            // Re-animating the same target replaces it; carry a running compositor-promotion across the swap so it isn't
            // lost (the node stays a motion node, and the LAST Release un-promotes it).
            if (entry.Channel == CompositorChannel.Transform && !entry.Promoted &&
                Entries.Find(e => ReferenceEquals(e.Target, target)) is { Promoted: true })
                entry.Promoted = true;
            Entries.RemoveAll(e => ReferenceEquals(e.Target, target));
            Entries.Add(entry);
        }
        return entry;
    }

    /// <summary>Stop compositing this target (the animation was cancelled or finished). Loop thread.</summary>
    public static void Release(AdamantiumComponent target)
    {
        lock (Gate)
        {
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                var e = Entries[i];
                if (!ReferenceEquals(e.Target, target)) continue;
                // Un-promote a node WE promoted, so a once-animated static node stops full-re-recording every frame; re-bake
                // it in world space now no matrix drives it. An intrinsic motion node (FlipTile, scroll viewport) is left be.
                if (e.Promoted && e.Owner != null)
                {
                    e.Owner.IsRenderMotionNode = false;
                    RenderDirty.MarkStructural(e.Owner);
                }
                Entries.RemoveAt(i);
            }
        }
    }

    public static void Reset()
    {
        lock (Gate) Entries.Clear();
    }

    /// <summary>Re-capture every entry's base from the live tree. Loop thread, once per frame: layout may have moved or
    /// resized a transformed element, or a theme swap may have recoloured an animating brush - the render thread reads only
    /// these captured bases, so this is where such changes reach it. Reading the live tree here is the loop thread's right.</summary>
    public static void RefreshBases()
    {
        lock (Gate)
        {
            foreach (var entry in Entries)
            {
                if (entry.Channel == CompositorChannel.Transform)
                {
                    entry.CurrentBasis = CaptureBasis((Transform)entry.Target, entry.Owner);
                }
                else if (((Brush)entry.Target).ConsumeBaseChange())
                {
                    // Only when the brush itself changed (a theme recolour) - not every frame, which would reset the dedup.
                    entry.PaintBase = ((Brush)entry.Target).CaptureBase();
                }
            }
        }
    }

    /// <summary>Recompose every composited animation for the instant it is called, into <paramref name="into"/> (the caller
    /// keeps and reuses that list, so a present allocates nothing). Called by the RENDER thread, once per present.
    /// False = nothing is composited, and <paramref name="into"/> is empty.</summary>
    public static bool Tick(List<Entry> into)
    {
        into.Clear();
        lock (Gate)
        {
            if (Entries.Count == 0) return false;
            into.AddRange(Entries);
        }

        foreach (var entry in into) entry.Recompose();
        return true;
    }

    /// <summary>Is the render thread currently playing this target? Then the loop thread must NOT re-invalidate the render
    /// for it: mirroring the value into the property system is bookkeeping for hit-testing and bindings, and the picture is
    /// already correct - marking it dirty would make the loop thread re-do, once per frame, the very work that was moved off
    /// it. That double payment is the whole difference between an animation that is composited and one that merely also runs
    /// somewhere else.</summary>
    public static bool Owns(AdamantiumComponent target)
    {
        lock (Gate)
        {
            foreach (var entry in Entries)
                if (ReferenceEquals(entry.Target, target))
                    return true;
            return false;
        }
    }

    /// <summary>The entry playing <paramref name="target"/>, or null if the render thread isn't playing it.</summary>
    public static Entry EntryFor(AdamantiumComponent target)
    {
        lock (Gate)
        {
            foreach (var entry in Entries)
                if (ReferenceEquals(entry.Target, target))
                    return entry;
            return null;
        }
    }

    private static Basis CaptureBasis(Transform transform, IUIComponent owner) =>
        new(transform.Values, owner.Bounds.Location, owner.RenderTransformOrigin, owner.RenderSize);
}
