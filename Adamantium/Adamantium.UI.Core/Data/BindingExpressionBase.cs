using System;

namespace Adamantium.UI.Core.Data;

public abstract class BindingExpressionBase
{
   public virtual bool HasError { get; protected set; }

   public virtual bool HasValidationError { get; protected set; }

   public bool IsDirty { get; set; }

   public BindingBase BindingBase { get; internal set;}

   public BindingStatus Status { get; internal set; }

   public IFundamentalUIComponent Target { get; set; }

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

   public abstract void EstablishConnection();
   public abstract void CloseConnection();

}
