using System;

namespace Adamantium.Engine.Effects
{
   public class PropertyKey: IEquatable<PropertyKey>
   {
      private readonly string name;

      private readonly int hashcode;

      /// <summary>
      /// Initializes a new instance of the <see cref="PropertyKey"/> class.
      /// </summary>
      /// <param name="name">The Name.</param>
      public PropertyKey(string name)
      {
         this.name = name ?? throw new ArgumentNullException("name");
         hashcode = name.GetHashCode();
      }

      /// <summary>
      /// Gets the Name.
      /// </summary>
      /// <value>The Name.</value>
      public string Name => name;

      public bool Equals(PropertyKey other)
      {
         if (ReferenceEquals(null, other))
         {
            return false;
         }
         if (ReferenceEquals(this, other))
         {
            return true;
         }
         return string.Equals(name, other.name);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj))
         {
            return false;
         }
         if (ReferenceEquals(this, obj))
         {
            return true;
         }
         var materialKey = obj as PropertyKey;
         if (materialKey == null)
         {
            return false;
         }
         return Equals(materialKey);
      }

      public override int GetHashCode()
      {
         return hashcode;
      }

      public static bool operator ==(PropertyKey left, PropertyKey right)
      {
         return Equals(left, right);
      }

      public static bool operator !=(PropertyKey left, PropertyKey right)
      {
         return !Equals(left, right);
      }

      public override string ToString()
      {
         return string.Format("{0}", name);
      }
   }

   /// <summary>
   /// A typed <see cref="PropertyKey"/>
   /// </summary>
   /// <typeparam name="T">Type of the value to associate with the key</typeparam>
   public class PropertyKey<T> : PropertyKey
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="PropertyKey" /> class.
      /// </summary>
      /// <param name="name">The Name.</param>
      public PropertyKey(string name)
         : base(name)
      {
      }
   }
}
