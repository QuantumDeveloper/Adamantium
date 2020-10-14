using System;
using Microsoft.Build.Framework;

namespace Adamantium.Engine.CompilerTask
{
   public static class TaskItemExtensions
   {
      public static T GetMetadata<T>(this ITaskItem taskItem, string name, T defaultValue)
      {
         var outputValue = taskItem.GetMetadata(name);
         T value = defaultValue;
         if (!string.IsNullOrEmpty(outputValue))
         {
            try
            {
               value = (T)Convert.ChangeType(outputValue, typeof(T));
            }
            catch (Exception)
            {
            }
         }
         return value;
      }
   }
}
