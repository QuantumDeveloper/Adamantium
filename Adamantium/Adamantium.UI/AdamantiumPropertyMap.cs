using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Adamantium.UI.Controls;

namespace Adamantium.UI;

/// <summary>
/// Track registered <see cref="AdamantiumProperty"/> instances;
/// </summary>
public static class AdamantiumPropertyMap
{
   /// <summary>
   /// Native registered properties by type.
   /// </summary>
   private static readonly Dictionary<Type, AdamantiumPropertyContainer> Registered = new Dictionary<Type, AdamantiumPropertyContainer>();

   /// <summary>
   /// Attached registered properties by type.
   /// </summary>
   private static readonly Dictionary<Type, AdamantiumPropertyContainer> Attached = new Dictionary<Type, AdamantiumPropertyContainer>(); 

   /// <summary>
   /// Get all attached <see cref="AdamantiumProperty"/>s registered by an owner
   /// </summary>
   /// <param name="owner"></param>
   /// <returns></returns>
   /// <exception cref="ArgumentNullException"></exception>
   public static IEnumerable<AdamantiumProperty> GetAttached(Type owner)
   {
      if (owner == null)
      {
         throw new ArgumentNullException(nameof(owner));
      }

      IEnumerable<AdamantiumProperty> list = null;
      lock (Attached)
      {
         if (Attached.TryGetValue(owner, out var value))
         {
            list = value.Properties;
         }
      }
      return list;
   }

   /// <summary>
   /// Get all <see cref="AdamantiumProperty"/>s on <see cref="DependencyComponent"/>
   /// </summary>
   /// <param name="o"></param>
   /// <returns></returns>
   /// <exception cref="ArgumentNullException"></exception>
   public static IEnumerable<AdamantiumProperty> GetRegistered(AdamantiumComponent o)
   {
      return GetRegistered(o.GetType());
   }

   /// <summary>
   /// Returns all <see cref="AdamantiumProperty"/>s registered on a type
   /// </summary>
   /// <param name="type"></param>
   /// <returns></returns>
   /// <exception cref="ArgumentNullException"></exception>
   public static IEnumerable<AdamantiumProperty> GetRegistered(Type type)
   {
      if (type == null)
      {
         throw new ArgumentNullException(nameof(type));
      }

      while (type != null)
      {
         // Ensure the type's static constructor has been run.
         RuntimeHelpers.RunClassConstructor(type.TypeHandle);

         lock (Registered)
         {
            if (Registered.TryGetValue(type, out var container))
            {
               foreach (var p in container.Properties)
               {
                  yield return p;
               }
            }
         }
         type = type.GetTypeInfo().BaseType;
      }
   }

   /// <summary>
   /// Returns registered <see cref="AdamantiumProperty"/> on a Type by property Name 
   /// </summary>
   /// <param name="type"></param>
   /// <param name="name"></param>
   /// <returns></returns>
   /// <exception cref="ArgumentNullException"></exception>
   /// <exception cref="ArgumentException"></exception>
   public static AdamantiumProperty FindRegistered(Type type, String name)
   {
      if (type == null)
      {
         throw new ArgumentNullException(nameof(type));
      }

      if (String.IsNullOrEmpty(name))
      {
         throw new ArgumentNullException(nameof(name));
      }

      var results = GetRegistered(type);

      foreach (var p in results)
      {
         if (p.Name == name)
         {
            return p;
         }
      }
      return null;
   }

   /// <summary>
   /// Returns registered <see cref="AdamantiumProperty"/> on a Type by property Name 
   /// </summary>
   /// <param name="type"></param>
   /// <param name="property"></param>
   /// <returns></returns>
   /// <exception cref="ArgumentNullException"></exception>
   /// <exception cref="ArgumentException"></exception>
   public static AdamantiumProperty FindRegistered(Type type, AdamantiumProperty property)
   {
      if (type == null)
      {
         throw new ArgumentNullException(nameof(type));
      }

      if (property == null)
      {
         throw new ArgumentNullException(nameof(property));
      }

      var results = GetRegistered(type);

      foreach (var p in results)
      {
         if (p == property)
         {
            return property;
         }
      }
      return null;
   }

   public static AdamantiumProperty FindRegistered(object o, AdamantiumProperty property)
   {
      return FindRegistered(o.GetType(), property);
   }

   public static bool IsRegistered(object o, AdamantiumProperty property)
   {
      return IsRegistered(o.GetType(), property);
   }

   public static bool IsRegistered(Type type, AdamantiumProperty property)
   {
      return FindRegistered(type, property) != null;
   }

   public static void Register(Type type, AdamantiumProperty property)
   {
      if (property == null)
      {
         throw new ArgumentNullException(nameof(property));
      }

      if (type == null)
      {
         throw new ArgumentNullException(nameof(type));
      }

      AdamantiumPropertyContainer container = null;

      lock (Registered)
      {
         if (Registered.TryGetValue(type, out var value))
         {
            container = value;
            if (!container.Exists(property.Name))
            {
               container.Add(property);
            }
            else
            {
               throw new ArgumentNullException(
                  $"Property {property.Name}is already registered for type {property.OwnerType}");
            }
         }
         else
         {
            container = new AdamantiumPropertyContainer(type);
            container.Add(property);
            Registered.Add(type, container);
         }
      }

      if (property.IsAttached)
      {
         lock (Attached)
         {
            if (Attached.TryGetValue(type, out container))
            {
               container.Add(property);
            }
            else
            {
               container = new AdamantiumPropertyContainer(type);
               container.Add(property);
               Attached.Add(type, container);
            }
         }
      }
   }
}