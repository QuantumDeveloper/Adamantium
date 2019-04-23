using System;
using System.Globalization;

namespace Adamantium.UI.Data
{
   /// <summary>
   /// Provides a way to apply custom logic to a simple binding.
   /// </summary>
   public interface IValueConverter
   {
      /// <summary>
      /// Converts a value from source to target
      /// </summary>
      /// <param name="value">Value produced by a binding source</param>
      /// <param name="targetType">The type of the binding target property</param>
      /// <param name="parameter">The converter parameter to use</param>
      /// <param name="culture">Culture to use in the converter</param>
      /// <returns>A converted value. If the method returns null, the valid null value is used</returns>
      object Convert(object value, Type targetType, object parameter, CultureInfo culture);

      /// <summary>
      /// Converts a value back from target to source
      /// </summary>
      /// <param name="value">Value produced by a binding source</param>
      /// <param name="targetType">The type of the binding target property</param>
      /// <param name="parameter">The converter parameter to use</param>
      /// <param name="culture">Culture to use in the converter</param>
      /// <returns>A converted value. If the method returns null, the valid null value is used</returns>
      object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
   }
}
