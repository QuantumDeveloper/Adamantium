using System.Collections.ObjectModel;
using Adamantium.UI.Core.MarkupExtensions;

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

   // The positional argument of {Binding <path>} maps here (the markup-extension default property).
   [DefaultProperty]
   public PropertyPath Path
   {
      get => path;
      set
      {
         path = value;
         PropertyPathChanged = true;
      }
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
         ElementName = ElementName,
      };
   }

   public BindingMode Mode { get; set; }
   public object Source { get; set; }

   /// <summary>Binds to a property on the element with this x:Name (resolved in the target's tree), instead of the
   /// DataContext - the WPF {Binding Path, ElementName=X} model. Ignored when <see cref="Source"/> is set.</summary>
   public string ElementName { get; set; }
   public IValueConverter Converter { get; set; }
   public object ConverterParameter { get; set; }
   public UpdateSourceTrigger UpdateSourceTrigger { get; set; }
}