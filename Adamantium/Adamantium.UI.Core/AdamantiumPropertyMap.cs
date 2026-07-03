using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Adamantium.UI.Core;

/// <summary>
/// Track registered <see cref="AdamantiumProperty"/> instances;
/// </summary>
public static class AdamantiumPropertyMap
{
   /// <summary>
   /// Native registered properties by type.
   /// </summary>
   private static readonly Dictionary<Type, AdamantiumPropertyContainer> Registered = new Dictionary<Type, AdamantiumPropertyContainer>();

   // Flattened (own + all base) property list per concrete type, resolved ONCE. GetRegistered used to walk the whole
   // type hierarchy - GetTypeInfo().BaseType + RunClassConstructor + a lock - on EVERY call, i.e. on every component
   // construction (the AdamantiumComponent ctor enumerates it). A virtualized list realizing a burst of items ran this
   // reflection walk thousands of times. Cached here; invalidated on any registration (static-init only, never hot).
   private static readonly ConcurrentDictionary<Type, AdamantiumProperty[]> Flattened = new();

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

      return Flattened.GetOrAdd(type, FlattenRegistered);
   }

   // Walks the type hierarchy ONCE (own + every base), running each level's static ctor so its properties are
   // registered, and returns the collected list. Cached by GetRegistered so this reflection walk runs once per type.
   private static AdamantiumProperty[] FlattenRegistered(Type type)
   {
      var list = new List<AdamantiumProperty>();
      while (type != null)
      {
         RuntimeHelpers.RunClassConstructor(type.TypeHandle);

         lock (Registered)
         {
            if (Registered.TryGetValue(type, out var container))
            {
               foreach (var p in container.Properties)
               {
                  list.Add(p);
               }
            }
         }
         type = type.GetTypeInfo().BaseType;
      }
      return list.ToArray();
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

   public static bool IsRegistered(object o, AdamantiumProperty property)
   {
      return IsRegistered(o.GetType(), property);
   }

   public static bool IsRegistered(Type type, AdamantiumProperty property)
   {
      if (type == null)
      {
         throw new ArgumentNullException(nameof(type));
      }

      if (property == null)
      {
         throw new ArgumentNullException(nameof(property));
      }

      if (property.IsRegisteredForType(type)) return true;
      
      while (type != null)
      {
         // Ensure the type's static constructor has been run.
         RuntimeHelpers.RunClassConstructor(type.TypeHandle);

         lock (Registered)
         {
            if (Registered.TryGetValue(type, out var container))
            {
               if (container.Exists(property.Name))
               {
                  property.AddRegisteredType(type);
                  return true;
               }
            }
         }
         type = type.GetTypeInfo().BaseType;
      }

      return false;
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

      // a new property changes the flattened list of this type AND any derived type
      Flattened.Clear();

      if (!property.IsAttached) 
         return;
      
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