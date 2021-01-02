using Adamantium.UI.Controls;

namespace Adamantium.UI.Data
{
   public abstract class BindingExpressionBase
   {
      public virtual bool HasError { get; protected set; }

      public virtual bool HasValidationError { get; protected set; }

      public bool IsDirty { get; set; }

      public BindingBase ParentBindingBase { get; }

      public BindingStatus Status { get; internal set; }

      public AdamantiumComponent Target { get; set; }

      public AdamantiumProperty TargetProperty { get; set; }

      public virtual void UpdateSource()
      { }

      public virtual void UpdateTarget()
      { }

   }
}
