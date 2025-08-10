using Adamantium.UI.Core.MarkupExtensions;

namespace Adamantium.UI.Core.Data;

public abstract class BindingBase: MarkupExtension, ICloneable
{
   public uint Delay { get; set; }

   public object FallbackValue { get; set; }

   public string StringFormat { get; set; }

   public object TargetNullValue { get; set; }
   
   public bool IsAsync { get; set; }
   
   public override object ProvideObject(MarkupContext context)
   {
      return CreateBindingExpression();
   }

   protected abstract BindingExpressionBase CreateBindingExpression();

   public abstract object Clone();
}