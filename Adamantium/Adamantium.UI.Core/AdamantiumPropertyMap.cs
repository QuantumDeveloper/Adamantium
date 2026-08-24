using System;
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

   // The same flattened set keyed by NAME, built beside Flattened and dropped with it.
   private static readonly ConcurrentDictionary<Type, Dictionary<string, AdamantiumProperty>> FlattenedByName = new();

   // Just the INHERITING properties of a type - a handful (DataContext, Foreground, FontSize) out of the seventy a
   // control registers. Re-parenting has to revisit exactly these, and it used to find them by walking all seventy and
   // asking each one's merged metadata: ~120us per attached element, which on a virtualized grid realizing thousands of
   // tiles was the single most expensive thing about putting one on screen. Dropped with Flattened, for the same reason.
   private static readonly ConcurrentDictionary<Type, AdamantiumProperty[]> Inheriting = new();

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

   /// <summary>The same set as <see cref="GetRegistered(Type)"/>, as the ARRAY it is stored as - so a caller that walks
   /// it per instance (the component constructor) neither goes through an interface nor asks its Count separately.</summary>
   internal static AdamantiumProperty[] GetRegisteredArray(Type type) => Flattened.GetOrAdd(type, FlattenRegistered);

   /// <summary>Drops the cached inheriting sets - a metadata override can add inheritance for a derived type.</summary>
   internal static void InvalidateInheriting() => Inheriting.Clear();

   /// <summary>The properties of <paramref name="type"/> whose metadata FOR THAT TYPE declares inheritance. Resolved once
   /// per type: which properties inherit is a fact about the type, not about the element or where it is being attached.</summary>
   internal static AdamantiumProperty[] GetInheriting(Type type) => Inheriting.GetOrAdd(type, static t =>
   {
      var all = Flattened.GetOrAdd(t, FlattenRegistered);
      var found = new List<AdamantiumProperty>();
      foreach (var property in all)
      {
         if (property.GetDefaultMetadata(t) is { Inherits: true }) found.Add(property);
      }

      return found.ToArray();
   });

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

      // First wins, exactly as the scan this replaces did: FlattenRegistered walks the CONCRETE type first and its bases
      // after, so the most-derived declaration of a name is the one kept.
      return FlattenedByName.GetOrAdd(type, BuildNameMap).GetValueOrDefault(name);
   }

   // The flattened set keyed by NAME. The scan this replaces compared strings down the whole list - about 60 of them for
   // a plain control, more for a deep one - and it sits on the hot path of every by-name write: a COMPILED TEMPLATE is
   // full of SetValue("BorderThickness", ...), and so is every Setter a style applies. Built once per type.
   private static Dictionary<string, AdamantiumProperty> BuildNameMap(Type type)
   {
      var map = new Dictionary<string, AdamantiumProperty>(StringComparer.Ordinal);
      foreach (var property in GetRegistered(type))
      {
         map.TryAdd(property.Name, property);
      }

      return map;
   }

   /// <summary>
   /// Resolve a property PATH to its <see cref="AdamantiumProperty"/>. A plain name (<c>Background</c>) is looked up on
   /// <paramref name="componentType"/>; a dotted ATTACHED form (<c>ScrollViewer.HorizontalScrollBarVisibility</c>, optionally
   /// wrapped in parentheses WPF-style) is resolved to the owner type's static <c>&lt;Name&gt;Property</c> field. Lets a
   /// TemplateBinding or a Setter target an attached property, not just a plain one.
   /// </summary>
   public static AdamantiumProperty ResolveProperty(Type componentType, string path)
   {
      if (string.IsNullOrEmpty(path)) return null;
      if (path.Length > 1 && path[0] == '(' && path[^1] == ')') path = path[1..^1];
      var dot = path.IndexOf('.');
      return dot < 0 ? FindRegistered(componentType, path) : ResolveAttached(path[..dot], path[(dot + 1)..]);
   }

   private static readonly ConcurrentDictionary<(string owner, string prop), AdamantiumProperty> AttachedByPath = new();

   // owner short-name + attached property name -> the AdamantiumProperty declared as a static `<prop>Property` field on the
   // owner type. The owner's DECLARING type is not stored on the property (attached props register OwnerType as the
   // attaches-to base), so we reflect the field instead. Cached: the assembly scan + reflection runs once per path.
   private static AdamantiumProperty ResolveAttached(string ownerName, string propName) =>
      AttachedByPath.GetOrAdd((ownerName, propName), static key =>
      {
         var owner = ResolveOwnerType(key.owner);
         if (owner == null) return null;
         RuntimeHelpers.RunClassConstructor(owner.TypeHandle);   // ensure the static field is initialized
         var field = owner.GetField(key.prop + "Property",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
         return field?.GetValue(null) as AdamantiumProperty;
      });

   private static readonly ConcurrentDictionary<string, Type> OwnerTypeByName = new();

   // A component type by its SHORT name, among loaded AdamantiumComponent-derived types. One-time scan per name (cached).
   private static Type ResolveOwnerType(string name) => OwnerTypeByName.GetOrAdd(name, static n =>
   {
      foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
      {
         Type[] types;
         try { types = asm.GetTypes(); } catch { continue; }
         foreach (var t in types)
         {
            if (t.Name == n && typeof(AdamantiumComponent).IsAssignableFrom(t)) return t;
         }
      }
      return null;
   });

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

      // The answer is memoised against the type ASKED ABOUT, not only the one that declares the property. A property is
      // declared once on a base (Styles, Triggers, DataContext on FundamentalUIComponent) and written on hundreds of
      // derived types; remembering only the declaring type left every one of those writes walking the base chain and
      // running RunClassConstructor at each level, under a lock - measured at ~330 ms of a tab's build.
      var asked = type;

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
                  property.AddRegisteredType(asked);
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
               throw new InvalidOperationException(
                  $"Property '{property.Name}' is already registered for type '{type.FullName}'.");
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
      FlattenedByName.Clear();
      Inheriting.Clear();

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