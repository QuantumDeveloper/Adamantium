using System;
using System.Collections.Generic;
using System.Reflection;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

/// <summary>
/// Represents a property that can be set throught methods such as: styling, data binding, animation and inheritance
/// </summary>
public sealed class AdamantiumProperty:IEquatable<AdamantiumProperty>
{
   public static readonly object UnsetValue = new Unset();

   private Dictionary<Type, PropertyMetadata> defaultValues; 

   private static Int32 nextPropertyId = 1;

   public Int32 PropertyId { get; }

   public String Name { get; }

   public Type PropertyType { get; }

   public Type OwnerType { get; private set; }

   public ValidateValueCallBack ValidateValueCallBack { get; }

   public Boolean IsAttached { get; private set; }

   public Boolean ReadOnly { get; private set; }


   public bool IsValidType(object value)
   {
      if (PropertyType == value.GetType())
      {
         return true;
      }
      return false;
   }


   public bool IsValidValue(object value)
   {
      return IsValidType(value);
   }

      
   public void OverrideMetadata(Type ownerType, PropertyMetadata metadata)
   {
      AddDefaultMetadata(ownerType, metadata);
   }

   private void AddDefaultMetadata(Type ownerType, PropertyMetadata metadata)
   {
      if (ownerType == null)
      {
         throw new ArgumentNullException(nameof(ownerType));
      }

      if (metadata == null)
      {
         throw new ArgumentNullException(nameof(metadata));
      }

      if (defaultValues.ContainsKey(ownerType))
      {
         throw new InvalidOperationException("Default value is already set for " + Name);
      }

      defaultValues.Add(ownerType, metadata);
   }

   /// <summary>
   /// Gets the default value for the property on the specified type.
   /// </summary>
   /// <param name="ownerType">The type.</param>
   /// <returns>The default value.</returns>
   public PropertyMetadata GetDefaultMetadata(Type ownerType)
   {
      if (ownerType == null)
      {
         throw new ArgumentNullException(nameof(ownerType));
      }

      while (ownerType != null)
      {
         if (defaultValues.TryGetValue(ownerType, out var result))
         {
            return result;
         }
         ownerType = ownerType.GetTypeInfo().BaseType;
      }

      return null;
   }

   public event EventHandler<AdamantiumPropertyChangedEventArgs> NotifyChanged;

   internal void OnChanged(AdamantiumPropertyChangedEventArgs e)
   {
      NotifyChanged?.Invoke(this, e);
   }

   protected AdamantiumProperty(String name, Type valueType, Type ownerType )
   {
      if (name.Contains("."))
      {
         throw new ArgumentException(" 'Name' could not contain periods");
      }

      defaultValues = new Dictionary<Type, PropertyMetadata>();
      IsAttached = false;
      ReadOnly = false;
      Name = name;
      PropertyType = valueType;
      OwnerType = ownerType;
      var metadata = new PropertyMetadata();
      if (PropertyType.IsValueType)
      {
         metadata.DefaultValue = Activator.CreateInstance(PropertyType);
      }
      AddDefaultMetadata(ownerType, metadata);
      PropertyId = nextPropertyId++;
   }

   protected AdamantiumProperty(String name, Type valueType, Type ownerType, PropertyMetadata metadata)
   {
      if (name.Contains("."))
      {
         throw new ArgumentException(" 'Name' could not contain periods");
      }
      defaultValues = new Dictionary<Type, PropertyMetadata>();
      IsAttached = false;
      ReadOnly = false;
      Name = name;
      PropertyType = valueType;
      OwnerType = ownerType;

      CheckType(valueType, metadata, name);

      AddDefaultMetadata(ownerType, metadata);

      PropertyId = nextPropertyId++;
   }

   protected AdamantiumProperty(String name, Type valueType, Type ownerType, PropertyMetadata metadata, ValidateValueCallBack validateValueCallBack)
   {
      if (name.Contains("."))
      {
         throw new ArgumentException(" 'Name' could not contain periods");
      }
      defaultValues = new Dictionary<Type, PropertyMetadata>();
      IsAttached = false;
      ReadOnly = false;
      Name = name;
      PropertyType = valueType;
      OwnerType = ownerType;
      ValidateValueCallBack = validateValueCallBack;

      CheckType(valueType, metadata, name);

      AddDefaultMetadata(ownerType, metadata);

      PropertyId = nextPropertyId++;
   }

   private static void CheckType(Type valueType, PropertyMetadata metadata, String name)
   {
      if (valueType.IsValueType && metadata.DefaultValue == null)
      {
         throw new ArgumentException(
            "Default value is null, but property type cannot be null for PropertyName " + name);
      }
      if (metadata.DefaultValue != null)
      {
         if (!FindType(valueType, metadata.DefaultValue.GetType()))
            throw new ArgumentException(
               "Default value is not of the same type as property type for PropertyName " + name);
      }
   }

   private static bool FindType(Type typeToCompare, Type typeToSearch)
   {
      Type tmpType = typeToSearch;
      while (tmpType != null)
      {
         if (tmpType == typeToCompare)
         {
            return true;
         }
         tmpType = tmpType.GetTypeInfo().BaseType;
      }
      return false;
   }

   public static AdamantiumProperty Register(String name, Type propertyType, Type ownerType)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType);

      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty Register(String name, Type propertyType, Type ownerType, PropertyMetadata propertyMetadata)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType, propertyMetadata);

      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty Register(String name, Type propertyType, Type ownerType, PropertyMetadata propertyMetadata, ValidateValueCallBack validateValueCallBack)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType, propertyMetadata, validateValueCallBack);

      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty RegisterAttached(String name, Type propertyType, Type ownerType)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType) {IsAttached = true};
      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty RegisterAttached(String name, Type propertyType, Type ownerType, PropertyMetadata propertyMetadata)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType, propertyMetadata)
      {
         IsAttached = true
      };
      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty RegisterAttached(String name, Type propertyType, Type ownerType, PropertyMetadata propertyMetadata, ValidateValueCallBack validateValueCallBack)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType, propertyMetadata,
         validateValueCallBack) {IsAttached = true};
      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty RegisterAttachedReadOnly(String name, Type propertyType, Type ownerType, PropertyMetadata propertyMetadata)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType, propertyMetadata)
      {
         IsAttached = true,
         ReadOnly = true
      };
      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty RegisterAttachedReadOnly(String name, Type propertyType, Type ownerType, PropertyMetadata propertyMetadata, ValidateValueCallBack validateValueCallBack)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType, propertyMetadata,
         validateValueCallBack)
      {
         IsAttached = true,
         ReadOnly = true
      };
      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty RegisterReadOnly(String name, Type propertyType, Type ownerType, PropertyMetadata propertyMetadata)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType, propertyMetadata)
      {
         ReadOnly = true
      };
      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public static AdamantiumProperty RegisterReadOnly(String name, Type propertyType, Type ownerType, PropertyMetadata propertyMetadata, ValidateValueCallBack validateValueCallBack)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType, propertyMetadata,
         validateValueCallBack)
      {
         ReadOnly = true
      };
      AdamantiumPropertyMap.Register(ownerType, property);

      return property;
   }

   public bool Equals(AdamantiumProperty other)
   {
      return other != null && PropertyId == other.PropertyId;
   }

   /// <inheritdoc/>
   public override bool Equals(object obj)
   {
      var p = obj as AdamantiumProperty;
      return p != null && Equals(p);
   }

   public override int GetHashCode()
   {
      return PropertyId;
   }

   public override string ToString()
   {
      return Name;
   }

   /// <summary>
   /// Tests two <see cref="AdamantiumProperty"/>s for equality.
   /// </summary>
   /// <param name="a">The first property.</param>
   /// <param name="b">The second property.</param>
   /// <returns>True if the properties are equal, otherwise false.</returns>
   public static bool operator ==(AdamantiumProperty a, AdamantiumProperty b)
   {
      if (ReferenceEquals(a, b))
      {
         return true;
      }
      else if (((object)a == null) || ((object)b == null))
      {
         return false;
      }
      else
      {
         return a.Equals(b);
      }
   }

   /// <summary>
   /// Tests two <see cref="AdamantiumProperty"/>s for unequality.
   /// </summary>
   /// <param name="a">The first property.</param>
   /// <param name="b">The second property.</param>
   /// <returns>True if the properties are equal, otherwise false.</returns>
   public static bool operator !=(AdamantiumProperty a, AdamantiumProperty b)
   {
      return !(a == b);
   }

   private class Unset
   {
      /// <summary>
      /// Returns the string representation of the <see cref="UnsetValue"/>.
      /// </summary>
      /// <returns>The string "(unset)".</returns>
      public override string ToString()
      {
         return "(unset)";
      }
   }
}