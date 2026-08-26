using System;
using System.Globalization;

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

   // TEMP (leak hunt): source-side subscriptions taken and given up. The SOURCE is the long-lived end (a view model
   // outlives every view built against it), so a hook that is never given up holds its expression - and through it the
   // element the expression targets.
   public static long SourceHooks, SourceUnhooks;

   // Lenient coercion: keeps the raw value when it can't be made to fit (used writing back to source).
   internal static object Coerce(object value, Type targetType)
      => TryCoerce(value, targetType, out var result) ? result : value;

   // Strict: false (result=null) when the value can't be made to fit, so the caller skips the assignment instead of
   // pushing something that would throw.
   internal static bool TryCoerce(object value, Type targetType, out object result)
   {
      result = value;
      if (value == null || targetType == null || targetType.IsInstanceOfType(value)) return true;
      if (targetType == typeof(string))
      {
         result = value.ToString();
         return true;
      }

      try
      {
         result = Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType, CultureInfo.CurrentCulture);
         return true;
      }
      catch
      {
         // What Convert.ChangeType cannot place still converts through the engine's TypeParser - the SAME conversion the
         // markup compiler runs, so a bound value and an authored attribute land alike (a string -> Brush/Geometry, a
         // double -> GridLength).
         var parsed = TypeCastFactory.CastFromString(value, targetType);
         if (parsed != AdamantiumProperty.UnsetValue)
         {
            result = parsed;
            return true;
         }

         result = null;
         return false;
      }
   }
}
