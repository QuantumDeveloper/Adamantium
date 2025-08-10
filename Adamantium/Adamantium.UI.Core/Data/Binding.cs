using System.Collections.ObjectModel;

namespace Adamantium.UI.Core.Data;

public class Binding : BindingBase
{
   public Binding()
   {
   }

   public Binding(string path)
   {
      Path = new PropertyPath(path);
   }

   internal bool PropertyPathChanged;
   private PropertyPath path;

   public PropertyPath Path
   {
      get => path;
      set
      {
         path = value;
         PropertyPathChanged = true;
      }
   }
   
   protected override BindingExpressionBase CreateBindingExpression()
   {
      throw new NotImplementedException();
   }

   public override object Clone()
   {
      return new Binding()
      {
         Mode = Mode,
         Source = Source,
         Converter = Converter,
         ConverterParameter = ConverterParameter,
         UpdateSourceTrigger = UpdateSourceTrigger,
         Path = Path,
         IsAsync = IsAsync,
         Delay = Delay,
         FallbackValue = FallbackValue,
         StringFormat = StringFormat,
         TargetNullValue = TargetNullValue,
      };
   }
   
   public BindingMode Mode { get; set; }
   public object Source { get; set; }
   public IValueConverter Converter { get; set; }
   public object ConverterParameter { get; set; }
   public UpdateSourceTrigger UpdateSourceTrigger { get; set; }
}