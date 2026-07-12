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
      _frozen = null;   // appearance changed -> the cached frozen snapshot is stale; ToFrozen() re-clones on next access
      Changed?.Invoke(this, EventArgs.Empty);
   }

   public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
      typeof (Double), typeof (Brush), new PropertyMetadata(1.0));

   public Double Opacity
   {
      get => GetValue<Double>(OpacityProperty);
      set { if (IsFrozen) return; SetValue(OpacityProperty, value); }
   }

   // --- Frozen snapshot (render/compositor-thread safety) -------------------------------------------------------------
   // A brush is an animatable AdamantiumComponent the UPDATE thread mutates in place. The render/applier path must NOT read
   // it live (so it can later run on a separate thread) - it reads an IMMUTABLE snapshot instead. ToFrozen() returns a
   // private, frozen clone of the SAME runtime type with the current values copied, so every `is SolidColorBrush` /
   // `.Color` / `.GradientStops` read in the bake path works UNCHANGED. The clone is cached here and invalidated on
   // RaiseChanged, so a theme brush shared by thousands of elements yields ONE frozen copy; and, since Brush has no value
   // Equals, returning the SAME instance for an unchanged brush also keeps the render cache's reference-equality change
   // detection stable (no spurious re-bake / text re-raster on a re-record). The frozen clone is never handed to control
   // code and its CLR setters are guarded, so nothing can mutate it.
   private Brush _frozen;
   private bool _isFrozen;

   public bool IsFrozen => _isFrozen;

   /// <summary>An immutable snapshot of this brush's current appearance, safe to read off the render thread; cached until
   /// the brush changes. A frozen brush returns itself.</summary>
   public Brush ToFrozen() => _isFrozen ? this : _frozen ??= CreateFrozenCore();

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