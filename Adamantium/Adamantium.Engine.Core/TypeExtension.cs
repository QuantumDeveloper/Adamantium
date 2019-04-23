using System;

namespace Adamantium.Engine.Core
{
   public static class TypeExtension
   {
      public static Boolean IsInstanceOfGenericType(this Type type, Type typeToCompare)
      {
         if (type.IsGenericType && type.GetGenericTypeDefinition() == typeToCompare)
         {
            return true;
         }
         return false;
      }

      public static bool InheritsFrom(this Type originalType, Type baseType)
      {
         var current = originalType.BaseType;

         while (current != null)
         {
            if (current == baseType)
            {
               return true;
            }

            current = current.BaseType;
         }

         return false;
      }
   }
}
