using System;

namespace Adamantium.UI.Core.Data;

public abstract class BindingExpressionBase
{
   public virtual bool HasError { get; protected set; }

   public virtual bool HasValidationError { get; protected set; }

   public bool IsDirty { get; set; }

   public BindingBase BindingBase { get; internal set;}

   public BindingStatus Status { get; internal set; }

   /// <summary>What the binding writes to. Any component with the property system, NOT only a tree element: a Transform
   /// is an AdamantiumComponent that carries animatable properties but sits outside the logical tree, and refusing to
   /// bind it made <c>&lt;Transform ScaleX="{Binding Zoom}"/&gt;</c> - the obvious markup - impossible. It reaches a
   /// DataContext through its InheritanceParent, which is what <see cref="DataContextSource"/> walks.</summary>
   public IAdamantiumComponent Target { get; set; }

   /// <summary>The nearest tree element at or above <see cref="Target"/> - the thing that actually has a DataContext to
   /// bind against, and the anchor for ElementName/ancestor lookups.</summary>
   public IFundamentalUIComponent DataContextSource => NearestElement(Target);

   internal static IFundamentalUIComponent NearestElement(IAdamantiumComponent component)
   {
      for (var node = component; node != null; node = node.InheritanceParent)
         if (node is IFundamentalUIComponent element) return element;

      return null;
   }

   public AdamantiumProperty TargetProperty { get; set; }

   // The value this expression currently produces (after its own converter, before any target-type coercion). It
   // matters only when the expression is a CHILD of a MultiBinding: its value feeds the parent's converter instead
   // of driving a target property. A top-level expression (TargetProperty != null) pushes straight to the target.
   public object ProducedValue { get; protected set; }

   // Raised when ProducedValue changes so a parent MultiBindingExpression can recombine. This is what makes
   // multibinding-inside-multibinding work: expressions nest as producers and bubble changes upward.
   public event Action<BindingExpressionBase> ValueChanged;

   protected void RaiseValueChanged() => ValueChanged?.Invoke(this);

   public virtual void UpdateSource()
   { }

   public virtual void UpdateTarget()
   { }

   // F2: queue this expression for the once-per-frame coalesced flush instead of pushing synchronously - unless the
   // binding opted into immediate application (a side-effect binding). Used for RUNTIME source changes; the initial
   // connect push stays synchronous.
   protected void ScheduleUpdate()
   {
      // Only a TOP-LEVEL binding (one that writes to a UI target) is batched. A producer (TargetProperty == null, a
      // MultiBinding child) feeds its parent synchronously - it's combinator plumbing, not a target write, and is also
      // exercised in isolation with no frame to flush it. IsImmediate opts a side-effect binding back to synchronous.
      if (TargetProperty == null || BindingBase?.IsImmediate == true) ApplyPending();
      else BindingUpdateQueue.Enqueue(this);
   }

   // F2: apply the pending (coalesced) update - reads the CURRENT source value and pushes it to the target. Called by
   // the per-frame BindingUpdateQueue flush; reading the latest value is what makes N source changes collapse to one.
   internal virtual void ApplyPending() => UpdateTarget();

   public abstract void EstablishConnection();
   public abstract void CloseConnection();

}
