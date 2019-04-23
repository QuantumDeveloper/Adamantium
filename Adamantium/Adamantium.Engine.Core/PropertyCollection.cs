using Adamantium.Core;
using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core
{
   public class CommonData
   {
      public class PropertyCollection : Dictionary<String, Object>
      {
         public void SetProperty<T>(PropertyKey<T> key, T value)
         {
            if (Utilities.IsEnum<T>(value))
            {
               var intValue = Convert.ToInt32(value);
               Add(key.Name, intValue);
            }
            else
            {
               Add(key.Name, value);
            }
         }

         public PropertyCollection Clone()
         {
            return (PropertyCollection) MemberwiseClone();
         }
      }
   }

   public class PropertyCollection : Dictionary<PropertyKey, object>
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="PropertyCollection"/> class.
      /// </summary>
      public PropertyCollection()
      {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="PropertyCollection" /> class that is empty, has the specified initial capacity, and uses the default equality comparer for the key type.
      /// </summary>
      /// <param name="capacity">The initial number of elements that the <see cref="PropertyCollection" /> can contain.</param>
      public PropertyCollection(int capacity)
         : base(capacity)
      {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="PropertyCollection"/> class.
      /// </summary>
      /// <param name="dictionary">The dictionary.</param>
      public PropertyCollection(IDictionary<PropertyKey, object> dictionary)
         : base(dictionary)
      {
      }

      public void SetProperty<T>(PropertyKey<T> key, T value)
      {
         if (Utilities.IsEnum(typeof(T)))
         {
            var intValue = Convert.ToInt32(value);
            Add(key, intValue);
         }
         else
         {
            Add(key, value);
         }
      }

      public bool ContainsKey<T>(PropertyKey<T> key)
      {
         return base.ContainsKey(key);
      }

      public T GetProperty<T>(PropertyKey<T> key)
      {
         object value;
         return TryGetValue(key, out value) ? Utilities.IsEnum(typeof(T)) ? (T)Enum.ToObject(typeof(T), (int)value) : (T)value : default(T);
      }

      public virtual PropertyCollection Clone()
      {
         return (PropertyCollection)MemberwiseClone();
      }

   }
}
