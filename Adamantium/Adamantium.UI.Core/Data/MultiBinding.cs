using System.Collections.ObjectModel;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// Binds one target property to several sources at once: each child in <see cref="Bindings"/> produces a value, and
/// <see cref="Converter"/> (an <see cref="IMultiValueConverter"/>) combines them into the target value. A child can be
/// a plain <see cref="Binding"/> or another <see cref="MultiBinding"/>, so multi-bindings nest arbitrarily. With no
/// converter, <see cref="BindingBase.StringFormat"/> is used instead. (One-way for now; <c>ConvertBack</c> wiring is
/// a later addition.)
/// </summary>
public class MultiBinding : BindingBase
{
   public Collection<BindingBase> Bindings { get; } = new();

   public BindingMode Mode { get; set; }

   public IMultiValueConverter Converter { get; set; }

   public object ConverterParameter { get; set; }

   public override object Clone()
   {
      var clone = new MultiBinding
      {
         Mode = Mode,
         Converter = Converter,
         ConverterParameter = ConverterParameter,
         StringFormat = StringFormat,
         FallbackValue = FallbackValue,
         TargetNullValue = TargetNullValue,
         IsAsync = IsAsync,
         Delay = Delay,
      };
      foreach (var binding in Bindings)
         clone.Bindings.Add((BindingBase)binding.Clone());
      return clone;
   }
}
