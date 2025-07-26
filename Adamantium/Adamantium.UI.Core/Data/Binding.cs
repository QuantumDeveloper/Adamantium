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

   public bool IsAsync { get; set; }
   public BindingMode Mode { get; set; }
   public object Source { get; set; }
   public IValueConverter Converter { get; set; }
   public object ConverterParameter { get; set; }

   protected override BindingExpressionBase CreateBindingExpression()
   {
      return BindingExpression.CreateBindingExpression();
   }

   public override object Clone()
   {
      ReadOnlyObservableCollection<Int32> col =
         new ReadOnlyObservableCollection<int>(new ObservableCollection<int>(new List<int>() { 15 }));

      return null;
   }
}