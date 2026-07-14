using System;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Media;

[TypeParser(typeof(BrushParser))]
public abstract class Brush: AdamantiumComponent
{
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

   protected void RaiseChanged()
   {
      // Re-PUBLISH the snapshot the render path reads (see Snapshot). Eagerly, here, on the thread that owns the brush -
      // a payload holds the LIVE brush and dereferences its current snapshot, so a stale one is a change that never
      // reaches the screen. Only for a brush that already has one: a clone being built inside CreateFrozenCore raises
      // this from its own initializer, and snapshotting THAT would recurse forever.
      if (_snapshot != null) _snapshot = CreateFrozenCore();
      Changed?.Invoke(this, EventArgs.Empty);
   }

   // PAINT: a brush's own opacity changes only the colour the units are baked with - never a shape, never a layout. Every
   // element painting with this brush re-bakes (via Changed -> InvalidatePaint); the flag STATES that, so an animation of
   // it can also be recognised as composited (run on the render thread) without the renderer keeping a hardcoded list of
   // "known" brush properties. The loading-skeleton pulse animates exactly this.
   public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
      typeof (Double), typeof (Brush), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint));

   public Double Opacity
   {
      get => GetValue<Double>(OpacityProperty);
      set { if (IsFrozen) return; SetValue(OpacityProperty, value); }
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
   private volatile Brush _snapshot;
   private bool _isFrozen;

   public bool IsFrozen => _isFrozen;

   /// <summary>The immutable snapshot of this brush's CURRENT appearance - what the bake/draw path reads. A frozen brush is
   /// its own snapshot. Null until the brush has been prepared for rendering, which every payload does in its constructor
   /// (see <see cref="ForRendering"/>), so a brush that can be drawn always has one.</summary>
   public Brush Snapshot => _isFrozen ? this : _snapshot;

   /// <summary>Prepare this brush to be drawn: publish a snapshot for the render thread, and hand back the LIVE brush -
   /// which is what a payload stores, so later changes stay visible through <see cref="Snapshot"/>. Called on the thread
   /// that owns the brush (recording), never on the render thread.</summary>
   public Brush ForRendering()
   {
      if (!_isFrozen) _snapshot ??= CreateFrozenCore();
      return this;
   }

   /// <summary>Build a fresh immutable clone of the current values. Subclasses copy their own properties and wrap the
   /// result in <see cref="AsFrozen{T}"/> to lock its setters.</summary>
   protected abstract Brush CreateFrozenCore();

   // Stamp a freshly-constructed clone immutable. Construction (ctor + object initializer) runs with the setters OPEN;
   // this closes them afterwards (each subclass setter early-returns when IsFrozen), so the clone can never change.
   protected static T AsFrozen<T>(T clone) where T : Brush
   {
      clone._isFrozen = true;
      return clone;
   }
}