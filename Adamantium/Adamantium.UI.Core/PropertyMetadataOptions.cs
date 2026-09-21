namespace Adamantium.UI.Core;

/// <summary>
/// A set of flags describing <see cref="AdamantiumProperty"/>  behavior
/// </summary>
[Flags]
public enum PropertyMetadataOptions
{
   /// <summary>
   /// No options
   /// </summary>
   None = 0,
   /// <summary>
   /// If BindingMode will not be set explicitly, default binding mode will set to TwoWay
   /// </summary>
   BindsTwoWayByDefault = 1,
      
   /// <summary>
   /// This property inherits value from the parent property in Logical Tree
   /// </summary>
   Inherits = 2,
      
   /// <summary>
   /// DataBinding cannot be set on particular <see cref="AdamantiumProperty"/> 
   /// </summary>
   NotDataBindable = 4,

   /// <summary>
   /// Definining this flag means that changing it will influnce on measure of current instance
   /// </summary>
   AffectsMeasure = 8,

   /// <summary>
   /// Definining this flag means that changing it will influnce on Parents measure
   /// </summary>
   AffectsParentMeasure = 16,

   /// <summary>
   /// Definining this flag means that changing it will influnce on arrange of current instance
   /// </summary>
   AffectsArrange = 32,

   /// <summary>
   /// Definining this flag means that changing it will influnce on Parents arrange
   /// </summary>
   AffectsParentArrange = 64,

   /// <summary>
   /// Defining this flag means that control geometry will be recteated if this property will changed
   /// </summary>
   AffectsRender = 128,

   /// <summary>
   /// Changing this property changes only WHAT the element is painted with - a colour, a brush, an opacity - never its
   /// shape and never its layout. The renderer re-bakes the GPU data of the units it already holds instead of re-recording
   /// the element (<see cref="IUIComponent.InvalidatePaint"/>), and an animation of such a property can be run by the
   /// COMPOSITOR, off the loop thread, because applying it needs neither layout nor a re-record.
   /// </summary>
   /// <remarks>
   /// Declared by whoever owns the property, so a third-party control's own colour property gets the cheap path and the
   /// composited animation with no change to the renderer - which is the point of stating it here rather than keeping a
   /// hardcoded list of known properties on the render side.
   ///
   /// Not for a property that REPLACES a brush object (Background, Fill, ...): a recorded draw command holds the brush it
   /// was recorded with BY REFERENCE, so swapping in a different brush still needs a re-record (AffectsRender). It is the
   /// brush's OWN values (a colour, an opacity, a gradient stop) that are paint.
   ///
   /// On a BRUSH's own properties the flag does not DELIVER the repaint - a Brush is not an IUIComponent, so the
   /// invalidation above is a no-op for it. The repaint arrives by another road: a brush raises Changed on any change of
   /// its own, and every element drawing with it is subscribed (AdamantiumComponent.OnAffectsRenderBrushChanged). What the
   /// flag does there is CLASSIFY the change - it is how the compositor knows this animation is colour-only and can run on
   /// the render thread. So mark every paint-only brush/stop property, but do not expect the mark to be what repaints it.
   /// </remarks>
   AffectsPaint = 256
}