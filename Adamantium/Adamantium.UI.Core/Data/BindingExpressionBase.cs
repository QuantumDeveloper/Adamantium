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
   
   public virtual void UpdateSource()
   { }

   public virtual void UpdateTarget()
   { }

   public abstract void EstablishConnection();
   public abstract void CloseConnection();

}