using System;

namespace Adamantium.UI.Data;

public abstract class BindingBase:ICloneable
{
   public int Delay { get; set; }

   public object FallbackValue { get; set; }

   public string StringFormat { get; set; }

   public object TargetNullValue { get; set; }

   public abstract object Clone();
}