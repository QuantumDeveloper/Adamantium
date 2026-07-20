# Responsiveness (UWP-style "never feels frozen") — Initiative Plan

**Status:** PARKED (design captured; not started). Raised while fixing the tab-transition first-entry snap.

## Problem

Heavy views (e.g. the Shapes gallery tab) hitch on entry, and the app *feels* frozen for that
window even though we have a decoupled render thread. Confirmed facts:

- The first-entry build of a heavy tab is one ~67 ms loop frame (measure + arrange + first bake).
- The tab element tree is **rebuilt on every entry** (no caching) → the hitch repeats every entry,
  not just the first. (User confirmed: lags on *every* entry.)
- The content-transition slide itself is timing-correct (loop-side advance is smooth, the stall-clamp
  works); the visible "too fast / skip" is the render not getting evenly-paced frames while the loop
  is busy building.

## Why it still feels frozen despite the render thread

UWP does **not** make the heavy build free — it also blocks its UI thread on a heavy build. It *feels*
smooth because (1) the composition thread is fully independent (scroll + composition animations keep
running while the UI thread is busy) and (2) `x:Load` / `DeferLoadStrategy` / `x:Phase` spread the
build over frames so the UI thread never hangs long.

Ours still hitches because the **loop thread does everything together**: input (`DrainPending`) +
style/measure/arrange + `RecordRenderFrame`. A heavy build blocks the loop, so for that frame:

- **input is not processed** (clicks/hover feel dead);
- **loop-driven (non-composited) animations stall** — they advance on the loop thread. Composited
  transform/paint animations (`Compositor`, Stopwatch-driven on the render thread) do NOT stall.

The compositor keeps presenting the last frame (no black screen), but input + non-composited
animations hitch. Our per-tree build may also be heavier than WPF's on an equal tree — needs measuring.

## Levers

- **A. Verify compositor independence during a loop stall.** Confirm the render thread keeps
  presenting + advancing composited animations while the loop is blocked in a build. (Mostly there by
  design — see RENDER_THREAD_PLAN.md — needs an explicit test/measurement.)
- **B. Composite the perceptually-critical animations.** Route the content-transition slide and scroll
  inertia through the `Compositor` (Stopwatch clock on the render thread) so a loop hitch can't stall
  them. Today only keyframe animations take over the compositor (`RunningKeyFrameAnimation` →
  `Compositor.TryTakeOver`); `DoubleAnimation` runs loop-side.
- **C. Incremental / deferred tree realization (the big one).** See below.
- **D. Input resilient to a build.** Consider processing input off the loop's build critical path so
  clicks land even on a heavy frame (harder; the loop owns the live tree).

## C — Incremental / deferred realization (design)

**Why the frame budget failed (do not retry it):** the old `LayoutManager` time budget sliced the
*layout pass* mid-way and re-queued the tail → it published TORN frames (a grid with tiles of two
sizes; a deferred arrange at its old rect while neighbours moved). You cannot slice an
already-built tree's layout by time. That budget was removed on purpose. See TECH_DEBT.md.

**The right lever is slicing the BUILD (element creation / intake), not the layout pass.** The engine
already does this for repeated items: a `VirtualizingPanel` realizes only the viewport + margin and
spreads a big realize over frames via `LayoutManager.InvalidateMeasureNextPass` ("bound the intake at
the source"). Incremental loading generalizes that to non-repeated subtrees.

**The framework cannot guess slice points for an arbitrary tree — the AUTHOR declares them, at the
template level** (exactly UWP's model: `x:Load`, `x:DeferLoadStrategy`, `x:Phase`). Two author-declared
mechanisms:

1. **Virtualization** (repeats) — *already exists.* Primary mechanism for large item collections.
2. **Deferred subtree** (`x:Load` analog) — *new.* An author marks a subtree as deferred (an attached
   `x:Load="False"` / a `<Deferred>` boundary element in the AUML). It is NOT instantiated with its
   parent; a lightweight placeholder/skeleton takes its slot. It is realized later by a trigger.

**"How many to build per frame" = a COUNT bound at the source, never a time budget.** The author's
deferral boundaries ARE the slice points; a background realizer drains them at most N boundaries (or
N elements) per idle frame. So the framework never guesses a count for an opaque tree — it realizes
author-declared boundaries, one/a-few per idle frame.

**Realization triggers** for a deferred boundary:
- progressive idle fill (skeleton first, then fill over idle frames, woken via `LoopSignal`);
- viewport proximity (realize as it nears the visible window — the virtualization signal, reused);
- explicit signal (e.g. `IsExpanded`, a tab becoming selected, a binding flipping `x:Load` true).

**Implementation sketch:**
- A `DeferredContent` boundary (or `x:Load` attached property) that holds its child's *factory* (the
  template/AUML fragment) instead of the built child; renders a placeholder until realized.
- A global **realize queue** drained on idle frames, bounded per frame by element/boundary COUNT (at
  the source, like `InvalidateMeasureNextPass`), signalled through `LoopSignal`.
- Realization builds the subtree, swaps out the placeholder, invalidates layout for just that boundary.
- Compose with B: while content realizes, composited animations/scroll stay smooth, so the app reads
  as responsive even though the tree is still filling in.

**Relationship to caching:** orthogonal. Caching (opt-in `KeepAlive` / a `NavigationCacheMode` analog,
default OFF so memory stays bounded) removes the *re-entry* rebuild cost; incremental realization makes
any *first* build non-blocking. A heavy, frequently-revisited tab wants both.

### C-alt — background build + splice (user proposal)

Instead of (or alongside) idle-slicing on the loop thread: when a subtree is marked deferred, build it
WHOLE on a background thread while a placeholder holds its slot; once built, splice it into the live
tree on the loop thread and drop the placeholder. Appeal: the loop never hitches on instantiation.

**Caveat — thread-safety of UI construction.** A UI tree is largely thread-affine here. What splits:
- **Background-safe (isolated, disjoint graph, NOT attached to the live tree):** element instantiation +
  local property writes. The property system locks/boxes, but the locks appear per-object, so a disjoint
  graph does not contend. This is often the bulk *by element count*.
- **Must stay on the loop/render thread (after splice):** style application (`ApplyCurrentTheme` reads the
  shared theme resource dictionaries), binding subscription (subscribes to shared sources), measure/arrange
  (mutate loop-read state), and the render bake (render thread + GPU resources).

So the background build offloads **instantiation**; splice + style + bind + measure/arrange/bake still land
on the loop. Whether this is a net win depends on the 67 ms breakdown (instantiation vs measure/arrange vs
bake). If the cost is mostly bake, background instantiation barely helps and the render-thread /
parallel-bake angle is the right lever instead — we already have `BeginDeferredInvalidation` + parallel
arrange for disjoint subtrees; the open crux is thread-safe bake (see the parallel-layout initiative).

**Therefore: MEASURE the 67 ms breakdown FIRST** (style/measure/arrange/bake), then decide between
idle-slice (C), background-build-splice (C-alt), and parallel-bake.

## Open questions

- Measure our per-tree build cost vs WPF on an equal tree (is our build genuinely heavier, or is it the
  bake?). Split the 67 ms into style/measure/arrange/bake.
- Does the compositor truly keep presenting during a multi-hundred-ms loop stall? (Lever A test.)
- Placeholder/skeleton API: automatic (measured empty box at the boundary's declared size) vs
  author-supplied skeleton content.
