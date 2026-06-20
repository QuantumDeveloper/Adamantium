using Adamantium.UI.Core.MarkupExtensions;

namespace Adamantium.UI.Core.Data;

public abstract class BindingBase: MarkupExtension, ICloneable
{
   public uint Delay { get; set; }

   public object FallbackValue { get; set; }

   public string StringFormat { get; set; }

   public object TargetNullValue { get; set; }

   public bool IsAsync { get; set; }

   // When the markup context gives us the target element + property, establish a live binding through the engine
   // (which dispatches to a BindingExpression or MultiBindingExpression). Without a usable target we just hand back
   // the binding object itself.
   public override object ProvideObject(MarkupContext context)
   {
      if (context?.TargetObject is IFundamentalUIComponent target && !string.IsNullOrEmpty(context.TargetPropertyName))
         return BindingEngine.SetBinding(target, context.TargetPropertyName, this);
      return this;
   }

   public abstract object Clone();
}
