using System;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Media;

[TypeParser(typeof(BrushParser))]
public abstract class Brush: AdamantiumComponent, IRenderAttachable
{
   // Set by RaiseChanged (a genuine property change), consumed by the compositor's per-frame RefreshBases. Lets a paint
   // animation re-capture its base ONLY when the brush actually changed (a theme recolour) instead of every loop frame -
   // so the render thread's dedup (see Compositor's paint tag) is not defeated by a base that is "re-captured" unchanged.
   // Loop-thread only (RaiseChanged and RefreshBases both run there); never touched by the render thread's PublishSnapshot.
   private bool _baseChanged = true;

   private bool _anchorConsidered;

   // How many render properties of each owner currently hold this brush. A Border whose Background AND BorderBrush both
   // point at one theme brush attaches TWICE; counting is what keeps attach and detach symmetric - notify once per
   // owner, and drop the subscription only when its LAST property lets go.
   //
   // Counted rather than scanned: "-= then +=" walks the whole invocation list, and a theme brush has thousands of
   // subscribers. Allocated lazily - most brushes have exactly one owner.
   private Dictionary<AdamantiumComponent, int> _owners;

   // The immutable appearance the render path reads - see Snapshot.
   private volatile Brush _snapshot;

   private bool _isFrozen;

   // PAINT: a brush's own opacity changes only the colour the units are baked with - never a shape, never a layout. Every
   // element painting with this brush re-bakes (via Changed -> InvalidatePaint); the flag STATES that, so an animation of
   // it can also be recognised as composited (run on the render thread) without the renderer keeping a hardcoded list of
   // "known" brush properties. The loading-skeleton pulse animates exactly this.
   public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
      typeof (Double), typeof (Brush), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint));

   protected Brush()
   {
      // Any property change on the brush itself (Opacity here; Color on a SolidColorBrush; StartPoint/EndPoint on a
      // gradient) changes how it paints, so notify. A gradient also raises Changed for its stops (see GradientBrush).
      PropertyChanged += (_, _) => RaiseChanged();
   }

   /// <summary>Raised when the brush's appearance changes - a property here, or (for a gradient) a stop's Offset/Color.
   /// An element that draws with the brush subscribes to this and re-renders; see AdamantiumComponent's AffectsRender
   /// handling, which keeps the element hooked to whatever brush its render property currently holds. This is what lets
   /// an ANIMATED brush (e.g. a looping shimmer sweeping a gradient) repaint without the element polling.</summary>
   public event EventHandler Changed;

   public Double Opacity
   {
      get => GetValue<Double>(OpacityProperty);
      set
      {
         if (IsFrozen)
         {
            return;
         }

         SetValue(OpacityProperty, value);
      }
   }

   public bool IsFrozen => _isFrozen;

   /// <summary>The immutable snapshot of this brush's CURRENT appearance - what the bake/draw path reads. A frozen brush is
   /// its own snapshot. Null until the brush has been prepared for rendering, which every payload does in its constructor
   /// (see <see cref="ForRendering"/>), so a brush that can be drawn always has one.</summary>
   public Brush Snapshot => _isFrozen ? this : _snapshot;

   /// <summary>How many times this brush's appearance has been REWRITTEN IN PLACE (see <see cref="RaiseChanged"/>). The
   /// render side keeps, per brush, the version it last baked into its retained slots; the two differing is the whole
   /// question "does anything on screen still show the old colour", asked in O(brushes in the scene) rather than by
   /// walking anything. Needed because an in-place recolour changes no property, adds no unit and moves no slot, so a
   /// frame that replays or patches has nothing else to notice it by - which is how a palette repaint reached the
   /// elements that happened to be re-recorded that frame and no others.</summary>
   public int PaintVersion { get; private set; }

   /// <summary>How many handlers are listening to <see cref="Changed"/>. This is what the owner counting exists to keep
   /// at one per owner, so it is what the test has to read - the hold count alone is satisfied by a broken attach that
   /// subscribes every time.</summary>
   internal int SubscriberCount => (Changed?.GetInvocationList().Length ?? 0) + (_owners?.Count ?? 0);

   /// <summary>Is this element in the owner map - the only thing this brush can tell when its colour changes. An
   /// element that PAINTS with a brush and is not in it hears nothing, which is what left every inherited Foreground
   /// in the previous variant's colour.</summary>
   internal bool IsOwnedBy(AdamantiumComponent component) => _owners?.ContainsKey(component) == true;

   protected void RaiseChanged()
   {
      // Re-PUBLISH the snapshot the render path reads (see Snapshot). Eagerly, here, on the thread that owns the brush -
      // a payload holds the LIVE brush and dereferences its current snapshot, so a stale one is a change that never
      // reaches the screen. Only for a brush that already has one: a clone being built inside CreateFrozenCore raises
      // this from its own initializer, and snapshotting THAT would recurse forever.
      if (_snapshot != null) _snapshot = CreateFrozenCore();
      PaintVersion++;
      _baseChanged = true;   // a real change to the brush's own values - the compositor re-captures its paint base on it

      // A wholesale discard happened since this brush last looked (see SweepGeneration). This is the moment it is worth
      // looking: a theme swap recolours every theme brush, so the ones that need sweeping are exactly the ones raising
      // this. One comparison on the hot path when there is nothing to do.
      if (_owners != null && _sweptGeneration != SweepGeneration) SweepOwnersOutOfTheTree();

      Changed?.Invoke(this, EventArgs.Empty);
      NotifyOwners();
   }

   // The owners are told through the MAP, not through Changed. They were subscribed to it as well, which made the same
   // fact live in two places and cost the difference between them: adding and removing a handler is O(subscribers) with
   // an array copy each time, and a theme brush is drawn with by every element in the window. Detaching one heavy tab -
   // 22251 nodes, each unsubscribing itself from lists tens of thousands long - measured at 3994 ms of a 4027 ms stall,
   // and the same quadratic ran on the way IN and on every raise of an animated brush. Through the map it is a
   // dictionary insert and remove, and the raise walks exactly the owners that exist.
   // Changed itself stays: a few non-owner subscribers (a GeometryDrawing holding this brush) genuinely need an event.
   private AdamantiumComponent[] _ownersSnapshot;

   private void NotifyOwners()
   {
      if (_owners == null || _owners.Count == 0) return;

      // Cached, because an animated brush raises this once a frame and an owner list must not be copied per raise.
      // Invalidated wherever the map changes; a handler that attaches or detaches during the walk therefore mutates the
      // map without disturbing the array being walked, which is what a snapshot is for.
      var owners = _ownersSnapshot;
      if (owners == null)
      {
         owners = new AdamantiumComponent[_owners.Count];
         _owners.Keys.CopyTo(owners, 0);
         _ownersSnapshot = owners;
      }

      foreach (var owner in owners) owner.OnRenderValueChanged(this, EventArgs.Empty);
   }

   // TEMP (leak hunt): how many owner LINKS have ever been taken and given up. Their difference is how many elements the
   // live brushes are holding right now - the number that says whether a release path runs at all, which reasoning about
   // the code cannot.
   public static long LinksTaken, LinksGivenUp;

   // The brushes that have ever been TAKEN by something - registered here the moment they take their first owner, and
   // weakly, so the register never keeps a brush alive. Only these can hold anything, and they are a fraction of all the
   // brushes there are, so nothing is paid for the many that are only ever drawn with once.
   //
   // A register is needed because the brushes that hold the discarded elements are the OLD theme's, and those raise
   // nothing after the swap - the theme is still in ThemeManager's map, its brushes are simply idle. Reaching them
   // through what CHANGES would reach exactly the wrong half.
   private static readonly List<WeakReference<Brush>> BrushesWithOwners = new();

   /// <summary>Everything that was discarded wholesale has now settled - look over every brush that holds owners and let
   /// go of the ones no longer in a tree. Called once per theme swap, off the settle signal.</summary>
   /// <summary>TEMP (leak hunt): brushes that still list a DESTROYED part as an owner, and how many such entries there
   /// are. The taken-minus-given counter said the links were flat and was wrong: a stale entry in one brush is balanced
   /// by a released one in another. Only counting the dead entries themselves says anything.</summary>
   public static (int Brushes, int DeadOwners, int LiveBrushes) DeadOwnerCensus()
   {
      List<Brush> live;
      lock (BrushesWithOwners)
      {
         live = new List<Brush>(BrushesWithOwners.Count);
         foreach (var handle in BrushesWithOwners)
            if (handle.TryGetTarget(out var brush)) live.Add(brush);
      }

      int brushes = 0, dead = 0;
      foreach (var brush in live)
      {
         var here = 0;
         if (brush._owners != null)
            foreach (var owner in brush._owners.Keys)
               if (owner is FundamentalUIComponent { IsDiscarded: true }) here++;

         // ...and the SUBSCRIBER LIST, which is a different thing from the owner map and can disagree with it. The map
         // came back clean while the graph showed this very event holding a destroyed part, so the map is not the
         // answer - the invocation list is.
         var handlers = brush.Changed?.GetInvocationList();
         if (handlers != null)
            foreach (var handler in handlers)
               if (handler.Target is FundamentalUIComponent { IsDiscarded: true }) here++;

         if (here > 0) { brushes++; dead += here; }
      }

      return (brushes, dead, live.Count);
   }

   public static void SweepEveryBrush()
   {
      System.Threading.Interlocked.Increment(ref SweepGeneration);

      List<Brush> live;
      lock (BrushesWithOwners)
      {
         live = new List<Brush>(BrushesWithOwners.Count);
         for (var i = BrushesWithOwners.Count - 1; i >= 0; i--)
         {
            if (BrushesWithOwners[i].TryGetTarget(out var brush)) live.Add(brush);
            else BrushesWithOwners.RemoveAt(i);
         }
      }

      foreach (var brush in live) brush.SweepOwnersOutOfTheTree();
   }

   void IRenderAttachable.AttachTo(AdamantiumComponent owner)
   {
      if (_owners == null)
      {
         _owners = new Dictionary<AdamantiumComponent, int>();
         lock (BrushesWithOwners) BrushesWithOwners.Add(new WeakReference<Brush>(this));
      }

      if (_owners.TryGetValue(owner, out var held))
      {
         _owners[owner] = held + 1;   // already subscribed for this owner; another of its properties took the brush
      }
      else
      {
         _owners[owner] = 1;
         System.Threading.Interlocked.Increment(ref LinksTaken);
         _ownersSnapshot = null;   // the map is what notifies them now - see RaiseChanged
      }

      Anchor(owner);

      // The sweep runs AFTER the pair is complete, and never in the middle of making it. Run before the subscribe, it
      // took out the very owner being attached - a template part is not in the tree yet while it is being built, so the
      // sweep reads it as gone - and the subscribe below then went ahead anyway. That leaves a SUBSCRIBER WITH NO MAP
      // ENTRY: invisible to every later sweep, and holding a whole discarded subtree through this brush. Measured at
      // +20 such a swap, with the owner map reading perfectly clean.
      if (_owners.Count > _sweepAt) SweepOwnersOutOfTheTree();
   }

   void IRenderAttachable.DetachFrom(AdamantiumComponent owner)
   {
      if (_owners == null || !_owners.TryGetValue(owner, out var held))
      {
         return;
      }

      if (held > 1)
      {
         _owners[owner] = held - 1;   // its other properties still draw with this brush
         return;
      }

      _owners.Remove(owner);
      System.Threading.Interlocked.Increment(ref LinksGivenUp);
      _ownersSnapshot = null;
   }

   // A brush outlives its owners - a THEME brush outlives the application - and it can only be TOLD an owner is gone by
   // that owner's property taking a different value. An element that is simply DISCARDED never does that, so its link
   // stayed and the brush held it: measured, +2080 elements a theme swap that a full collection could not reclaim, and
   // because they are all chained through the SAME shared brushes the retained set is one CONNECTED web - freeing a
   // fraction of the links frees nothing at all.
   //
   // So the brush asks instead of waiting to be told, and it asks the only question that has an answer: is this owner in
   // a live tree? Not weakly-referenced, deliberately: Changed is raised on every mutation of the brush and once a FRAME
   // for an animated one, and a weak subscriber list would have to be walked and dereferenced on every raise.
   //
   // AMORTIZED - the walk runs when the map has doubled since the last one, so a brush with thousands of owners pays
   // O(1) per attach. And it releases the owner WHOLE (every brush it holds, not just this one) rather than snipping one
   // link, because that is the call that also arms the re-take: an element that is merely between trees - a template
   // being built, a closed popup, a container waiting to be recycled - takes its brushes back when it is attached.
   private int _sweepAt = 16;

   /// <summary>Bumped when something discards elements WHOLESALE - a theme swap rebuilds every template in the
   /// application at once. Growth alone is not a good enough trigger: a brush whose owner count happens to come out the
   /// same after a swap as before it would never sweep, and ONE surviving link is enough to hold the whole web, because
   /// the discarded elements are all chained to each other through these very brushes. Measured: a doubling trigger
   /// alone left a quarter of the orphans holding, and freed no memory at all.</summary>
   public static int SweepGeneration;

   private int _sweptGeneration = -1;

   private void SweepOwnersOutOfTheTree()
   {
      _sweptGeneration = SweepGeneration;
      List<AdamantiumComponent> gone = null;
      foreach (var owner in _owners.Keys)
      {
         // Only an ELEMENT can be judged: a non-visual owner (a drawing, a stop, another brush) has no tree to be out
         // of, so it is left alone. PARKED is out of the tree ON PURPOSE and coming back - not gone.
         // DISCARDED counts as gone even when the tree still says otherwise: a part destroyed with its template is not
         // always DETACHED first, so RootVisual can still be set and "is it attached" answers yes for something that no
         // longer exists. That answer kept these owners on the list through every sweep - and one live control holding
         // this brush then held, through this very subscriber list, a whole discarded subtree.
         if (owner is FundamentalUIComponent { IsDiscarded: true } ||
             owner is IUIComponent { IsAttachedToVisualTree: false, IsParked: false })
         {
            (gone ??= new List<AdamantiumComponent>()).Add(owner);
         }
      }

      // Collected first: releasing mutates the very map being walked.
      _sweepAt = Math.Max(16, _owners.Count * 2);
      if (gone == null) return;

      foreach (var owner in gone)
      {
         // THIS brush lets go of THIS owner, by hand. Going through the owner's own release walk was wrong and hid the
         // rest of the leak for hours: that walk detaches whatever the owner's properties hold NOW, and by the time a
         // swap has settled they hold the NEW theme's brushes - so the detach landed on the wrong brush, and this one
         // kept both its map entry and its Changed subscription. The link counter said nothing, because a stale entry
         // here was balanced by a released one there.
         if (_owners.Remove(owner))
         {
            System.Threading.Interlocked.Increment(ref LinksGivenUp);
            _ownersSnapshot = null;
         }

         // ...and the owner still gives up the rest of what it holds, which also arms its re-take on attach.
         owner.ReleaseRenderAttachments();
      }

      _sweepAt = Math.Max(16, _owners.Count * 2);
   }

   /// <summary>How many of <paramref name="owner"/>'s render properties currently hold this brush.</summary>
   internal int OwnerHoldCount(AdamantiumComponent owner) =>
      _owners != null && _owners.TryGetValue(owner, out var held) ? held : 0;

   /// <summary>Hang this brush on the element that draws with it, so expressions written ON THE BRUSH -
   /// <c>{Binding Colour}</c>, <c>{ResourceReference Key}</c> - have a tree to resolve against. On its own a brush is
   /// not in the tree, so the lookup walks up from it, finds no element and yields null SILENTLY.
   /// <para>The FIRST owner wins, and only a brush that is actually WAITING on something is anchored: an anchor pins
   /// that element for as long as the brush lives, and a theme brush shared by thousands of recycled rows would
   /// otherwise hold whichever one used it first. Every later assignment costs one bool.</para></summary>
   private void Anchor(AdamantiumComponent owner)
   {
      if (_anchorConsidered)
      {
         return;
      }

      _anchorConsidered = true;

      // TWO places to look: an EXPRESSION on one of the brush's properties, and a RESOURCE only a tree-scoped lookup can
      // answer. Asking about the first alone is how {ResourceReference} on a brush kept resolving to nothing.
      var hasExpressions = Data.BindingEngine.HasBindings(this);
      var hasResources = Resources.ResourceResolver.HasPending(this);
      if (!hasExpressions && !hasResources)
      {
         return;
      }

      InheritanceParent = owner;

      if (hasExpressions)
      {
         Data.BindingEngine.RefreshBindings(this);

         // The refresh above searches from an element that markup has not added to its parent yet, so nothing that has
         // to be looked UP can be found. Two later moments each answer half of it, and BOTH are needed:
         //   * ATTACH - the element now has ancestors, so {Binding ElementName=...} can find its target;
         //   * the owner's DATACONTEXT arriving - an INHERITED DataContext is pushed down without announcing itself per
         //     descendant, and it lands AFTER attach, so a plain {Binding Path} still had nothing to read at attach.
         if (owner is IUIComponent visual)
         {
            visual.AttachedToVisualTreeEvent += (_, _) => Data.BindingEngine.RefreshBindings(this);
         }

         owner.PropertyChanged += (_, e) =>
         {
            if (e.Property == FundamentalUIComponent.DataContextProperty) Data.BindingEngine.RefreshBindings(this);
         };
      }

      if (hasResources)
      {
         Resources.ResourceResolver.Resolve(this, owner as IUIComponent);
      }
   }

   /// <summary>Returns whether the brush's own values changed since the last call, and clears the flag. Loop thread.</summary>
   public bool ConsumeBaseChange()
   {
      var changed = _baseChanged;
      _baseChanged = false;
      return changed;
   }

   // --- Frozen snapshot (render/compositor-thread safety) -------------------------------------------------------------
   // A brush is an animatable AdamantiumComponent the UPDATE thread mutates in place. The render/applier path must NOT read
   // it live (so it can run on a separate thread) - it reads an IMMUTABLE snapshot instead: a private, frozen clone of the
   // SAME runtime type with the current values copied, so every `is SolidColorBrush` / `.Color` / `.GradientStops` read in
   // the bake path works UNCHANGED. The clone is never handed to control code and its CLR setters are guarded, so nothing
   // can mutate it.
   //
   // The snapshot is PUBLISHED BY THE WRITER (RaiseChanged, above) and only ever READ by the render thread - a reference
   // swap of an immutable object, so the reader always sees one whole, self-consistent appearance and never touches the
   // live one. A payload therefore holds the LIVE brush and dereferences Snapshot per read; a payload that held the frozen
   // clone directly would pin the appearance the brush had WHEN IT WAS RECORDED - which is precisely why an animated brush
   // (the shimmer sweeping its gradient stops, a pulsing skeleton) repainted nothing: the paint re-bake faithfully re-baked
   // a snapshot from minutes ago. One clone per CHANGE, not per user: a theme brush shared by thousands of elements
   // publishes ONE. And an unchanged brush keeps handing out the SAME instance, so the render cache's reference-equality
   // change detection stays stable (no spurious re-bake / text re-raster on a re-record).

   /// <summary>Prepare this brush to be drawn: publish a snapshot for the render thread, and hand back the LIVE brush -
   /// which is what a payload stores, so later changes stay visible through <see cref="Snapshot"/>. Called on the thread
   /// that owns the brush (recording), never on the render thread.</summary>
   public Brush ForRendering()
   {
      if (!_isFrozen) _snapshot ??= CreateFrozenCore();
      return this;
   }

   private Brush CreateFrozenCore() => AsFrozen(CreateClone());

   /// <summary>A fresh, UNFROZEN clone of this brush's current values (same runtime type). Subclasses copy their own
   /// properties; the base freezes it. Split out from freezing so the compositor can override an animated value on the
   /// clone BEFORE it is frozen (see <see cref="BuildAnimatedSnapshot"/>).</summary>
   protected abstract Brush CreateClone();

   // --- Composited paint (render-thread animation) ------------------------------------------------------------------
   /// <summary>Build the animated snapshot the COMPOSITOR publishes each present: a frozen clone of this brush with the
   /// curve's paint tracks applied. Called on the render thread FROM A FROZEN BASE (see Compositor's paint entry), so it
   /// reads only immutable values - the live brush's own properties are never touched and stay at their base.
   ///
   /// General over ANY animatable brush property: the clone is UNFROZEN until <see cref="AsFrozen{T}"/>, so its setters
   /// work, and the curve only ever produces doubles - so setting each track's value covers Opacity (the skeleton pulse),
   /// a gradient radius, and any future double paint property with no per-property code. The AffectsPaint contract - a
   /// re-bake of what is recorded, never a re-record - is what makes this safe for every such property.</summary>
   public Brush BuildAnimatedSnapshot(AnimationCurve curve, double elapsed)
   {
      var clone = CreateClone();
      foreach (var track in curve.Tracks)
         clone.SetValue(track.Property, curve.Evaluate(track, elapsed));
      return AsFrozen(clone);
   }

   /// <summary>The animated value's VISIBLE resolution for the paint dedup (see Compositor): the coarsest quantum at which
   /// a change still cannot alter a pixel, so two instants that quantize equal need no re-bake. A colour/opacity value lands
   /// in an 8-bit channel, so 1/256 is EXACT. A geometric value (a gradient radius, relative to the filled bounds 0..1) has
   /// no size-independent quantum - a shared brush paints elements of every size - so 1/4096 is used: sub-pixel down to a
   /// 4K-wide element, and a geometric paint animation is rare and usually small, so the extra re-bakes cost nothing.</summary>
   public virtual double PaintQuantum(AdamantiumProperty property) => property == OpacityProperty ? 256.0 : 4096.0;

   /// <summary>A frozen clone of this brush's CURRENT (live, base) values - what the compositor captures on the loop thread
   /// as the base its animated snapshots are built from. Distinct from <see cref="Snapshot"/>, which the compositor itself
   /// overwrites while it animates: the base must stay the brush's own values so a theme recolour flows through, and reading
   /// Snapshot for it would feed the animated value back in and spiral.</summary>
   public Brush CaptureBase() => AsFrozen(CreateClone());

   /// <summary>Swap in the snapshot the render path reads. The compositor calls this from the render thread; it is a single
   /// volatile reference write, so a reader always sees one whole, self-consistent brush. See <see cref="Snapshot"/>.</summary>
   public void PublishSnapshot(Brush snapshot) => _snapshot = snapshot;

   // Stamp a freshly-constructed clone immutable. Construction (ctor + object initializer) runs with the setters OPEN;
   // this closes them afterwards (each subclass setter early-returns when IsFrozen), so the clone can never change.
   protected static T AsFrozen<T>(T clone) where T : Brush
   {
      clone._isFrozen = true;
      return clone;
   }
}
