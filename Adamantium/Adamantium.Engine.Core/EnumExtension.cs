using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Adamantium.Engine.Core
{
   public static class EnumExtension
   {
      public static T[] Sort<T>(this Enum values)
      {
         Type myEnumType = typeof(T);
         var enumNames = Enum.GetNames(myEnumType);
         List<int> enumPositions = new List<int>();
         //int[] enumPositions = Array.ConvertAll(enumNames, n =>
         //{
         //   OrderAttribute orderAttr = (OrderAttribute)myEnumType.GetField(n)
         //       .GetCustomAttributes(typeof(OrderAttribute), false)[0];
         //   return orderAttr.Order;
         //});


         List<Enum> sorted = new List<Enum>();
         foreach (Enum value in Enum.GetValues(values.GetType()))
         {
            if (values.HasFlag(value))
            {
               sorted.Add(value);
               //OrderAttribute orderAttr = (OrderAttribute)myEnumType.GetField(value.ToString())
               // .GetCustomAttributes(typeof(OrderAttribute), false)[0];
               //enumPositions.Add(orderAttr.Order);
            }
         }
         var enumValues = sorted.ToArray();
         //Array.Sort(enumPositions.ToArray(), enumValues);

         return enumValues.Cast<T>().ToArray();
      }

      public static T[] Sort<T>(this IEnumerable<Enum> enums)
      {
         Type myEnumType = typeof(T);
         var enumValues = Enum.GetValues(myEnumType).Cast<T>().ToArray();
         var enumNames = Enum.GetNames(myEnumType);
         int[] enumPositions = Array.ConvertAll(enumNames, n =>
         {
            OrderAttribute orderAttr = (OrderAttribute)myEnumType.GetField(n)
                .GetCustomAttributes(typeof(OrderAttribute), false)[0];
            return orderAttr.Order;
         });

         Array.Sort(enumPositions, enumValues);

         return enumValues;
      }

      public static String GetDescription (this Enum value)
      {
         string description = string.Empty;
         var attributes = value.GetType().GetCustomAttributes(typeof (DescriptionAttribute), false).Cast<DescriptionAttribute>();
         foreach (var attribute in attributes)
         {
            description += attribute.Description;
         }

         if (String.IsNullOrEmpty(description))
         {
            description = value.ToString();
         }

         return description;
      }
   }
}
