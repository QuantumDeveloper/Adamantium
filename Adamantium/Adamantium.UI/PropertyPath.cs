using System.Collections.ObjectModel;

namespace Adamantium.UI;

public sealed class PropertyPath
{
   public PropertyPath(object parameter)
   {
      Path = parameter.ToString();
   }

   public PropertyPath(string path, params object[] pathParameters)
   {
      Path = path;
      PathParameters = new Collection<object>(pathParameters);
   }

   public string Path { get; set; }

   public Collection<object> PathParameters { get; } 
}