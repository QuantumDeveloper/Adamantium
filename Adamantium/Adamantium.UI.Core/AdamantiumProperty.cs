using System.Reflection;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

/// <summary>
/// Represents a property that can be set through methods such as: styling, data binding, animation and inheritance
/// </summary>
public sealed class AdamantiumProperty:IEquatable<AdamantiumProperty>
{
   public static readonly object UnsetValue = new Unset();

   private Dictionary<Type, PropertyMetadata> defaultValues;

   // Memoises GetDefaultMetadata(concreteType): the raw resolve walks the base-type chain via reflection
   // (GetTypeInfo().BaseType) until it finds an entry in `defaultValues`, and it ran on EVERY property read + write (the
   // inherit branch of GetValue, and RunSetValueSequence) - several reflection hops per access for a deeply-derived type
   // (e.g. Border). Keyed by the concrete type; invalidated whenever defaultValues changes (registration/OverrideMetadata,
   // both static-init-time only, so this Clear never runs hot).
   private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyMetadata> metadataCache = new();

   private HashSet<Type> registeredTypes;

   private static Int32 nextPropertyId = 1;

   public Int32 PropertyId { get; }

   public String Name { get; }

   public Type PropertyType { get; }

   /// <summary>Whether a value of this property can need to know which element draws with it (see
   /// <see cref="IRenderAttachable"/>). Decided ONCE, at registration, from the DECLARED type - so a property that can
   /// never carry such a value (Visibility, ZIndex, a corner radius, a thickness) is never asked again, and the write
   /// path knows nothing about any of it.</summary>
   public bool CanAttachToOwner { get; private set; }

   /// <summary>Whether ANY type declares this property as inheriting. A value that is never inherited has nothing to say
   /// to a child, so a write of it must not wake one - and that is the common case: a handful of properties inherit
   /// (DataContext, Foreground, FontSize) and hundreds do not. Set at registration/override, so the write path answers
   /// it with a field read.</summary>
   public bool CanInherit { get; private set; }

   /// <summary>The INHERITANCE EPOCH: bumped whenever anything that can change what a descendant inherits happens - an
   /// explicit write of an inheriting property anywhere, or a re-parenting. A cached inherited value is good while its
   /// container carries the current epoch and is re-resolved from the ancestors when it does not.
   /// <para>Deliberately GLOBAL rather than per property or per subtree: the bump has to be O(1) (it sits on the write
   /// path), and a stale stamp costs one ancestor walk on the next READ of that one property - measured at ~0.7 us.
   /// Being coarse makes it re-walk a little more often; being cheap is what lets the write side stop pushing a value
   /// into every descendant it has.</para></summary>
   internal static long InheritanceEpoch;

   internal static void BumpInheritanceEpoch() => System.Threading.Interlocked.Increment(ref InheritanceEpoch);

   // Wiring, done once per property that can carry an attachable value. It rides the per-property Changed hook the
   // property system raises anyway, which is why setting a value costs nothing for this: no branch, no type test, no
   // mention of brushes - or of anything else that may later want an owner - anywhere in AdamantiumComponent.
   private void WireOwnerAttachment()
   {
      if (!CanAttachToOwner)
      {
         return;
      }

      Changed += static (sender, e) =>
      {
         if (sender is not AdamantiumComponent owner)
         {
            return;
         }

         (e.OldValue as IRenderAttachable)?.DetachFrom(owner);
         (e.NewValue as IRenderAttachable)?.AttachTo(owner);
      };
   }

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

   internal bool IsRegisteredForType(Type type)
   {
      return registeredTypes.Contains(type);
   }

   internal void AddRegisteredType(Type type)
   {
      registeredTypes.Add(type);
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

      // One declaration per type; a DERIVED type overrides, and its override layers over this one.
      if (defaultValues.ContainsKey(ownerType))
      {
         throw new InvalidOperationException(
            $"Metadata for property '{Name}' is already declared for type '{ownerType.FullName}'.");
      }

      defaultValues.Add(ownerType, metadata);
      // Once ANY declaration inherits, the property can inherit - an override may add it for a derived type and never
      // takes it away for the type that had it.
      CanInherit |= metadata.Inherits;
      metadataCache.Clear();   // a previously-resolved (base-walked) entry may now resolve to this newer, more-derived one
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

      return metadataCache.GetOrAdd(ownerType, ResolveDefaultMetadata);
   }

   // Every declaration in the type's ancestry, folded base-first (see PropertyMetadata.MergedWith). Folded here and not
   // at OverrideMetadata time: static constructors run in no guaranteed order, so a base's declaration may not exist yet.
   private PropertyMetadata ResolveDefaultMetadata(Type ownerType)
   {
      List<PropertyMetadata> chain = null;
      PropertyMetadata nearest = null;

      for (var type = ownerType; type != null; type = type.GetTypeInfo().BaseType)
      {
         if (!defaultValues.TryGetValue(type, out var declared)) continue;

         if (nearest == null)
         {
            nearest = declared;   // one declaration is the common case - do not allocate for it
            continue;
         }

         (chain ??= [nearest]).Add(declared);
      }

      if (chain == null)
         return nearest;

      // Most-derived first, so fold from the base end.
      var merged = chain[^1];
      for (var i = chain.Count - 2; i >= 0; i--)
      {
         merged = chain[i].MergedWith(merged);
      }

      return merged;
   }

   /// <summary>
   /// Global per-property change hook (Avalonia-style class handler). Raised for EVERY change of THIS property on ANY
   /// component, with <c>sender</c> = the COMPONENT that changed (so a handler knows the source and can filter by
   /// type/instance) and <c>e</c> carrying the property + old/new value.
   /// <para>Intended for a BOUNDED set of cross-cutting handlers - subscribe ONCE (typically per control type in a static
   /// ctor), then filter by sender. NEVER add one handler per live instance: this event fires for ALL instances, so a
   /// per-instance subscription re-creates an O(live-instances) fan-out on every set - the exact anti-pattern that made a
   /// templated list's scroll O(N). For reacting to a change on a SPECIFIC object, use that instance's
   /// <see cref="AdamantiumComponent.PropertyChanged"/> (which <see cref="Data.TemplateBindingExpression"/> now uses).</para>
   /// </summary>
   public event EventHandler<AdamantiumPropertyChangedEventArgs> Changed;

   /// <param name="source">The component whose property changed - passed as the event <c>sender</c> so handlers have the
   /// source's identity (the missing piece that previously forced blind reactions).</param>
   internal void RaiseChanged(object source, AdamantiumPropertyChangedEventArgs e)
   {
      Changed?.Invoke(source, e);
   }

   private AdamantiumProperty(String name, Type valueType, Type ownerType )
   {
      if (name.Contains("."))
      {
         throw new ArgumentException(" 'Name' could not contain periods");
      }

      registeredTypes = new HashSet<Type>();
      defaultValues = new Dictionary<Type, PropertyMetadata>();
      IsAttached = false;
      ReadOnly = false;
      Name = name;
      PropertyType = valueType;
      CanAttachToOwner = typeof(IRenderAttachable).IsAssignableFrom(valueType);
      WireOwnerAttachment();
      OwnerType = ownerType;
      var metadata = new PropertyMetadata();
      if (PropertyType.IsValueType)
      {
         metadata.DefaultValue = Activator.CreateInstance(PropertyType);
      }
      AddDefaultMetadata(ownerType, metadata);
      PropertyId = nextPropertyId++;
   }

    private AdamantiumProperty(String name, Type valueType, Type ownerType, PropertyMetadata metadata)
   {
      if (name.Contains("."))
      {
         throw new ArgumentException("'Name' could not contain periods");
      }
      registeredTypes = new HashSet<Type>();
      defaultValues = new Dictionary<Type, PropertyMetadata>();
      IsAttached = false;
      ReadOnly = false;
      Name = name;
      PropertyType = valueType;
      CanAttachToOwner = typeof(IRenderAttachable).IsAssignableFrom(valueType);
      WireOwnerAttachment();
      OwnerType = ownerType;

      CheckType(valueType, metadata, name);

      AddDefaultMetadata(ownerType, metadata);

      PropertyId = nextPropertyId++;
   }

    private AdamantiumProperty(String name, Type valueType, Type ownerType, PropertyMetadata metadata, ValidateValueCallBack validateValueCallBack)
   {
      if (name.Contains("."))
      {
         throw new ArgumentException(" 'Name' could not contain periods");
      }
      registeredTypes = new HashSet<Type>();
      defaultValues = new Dictionary<Type, PropertyMetadata>();
      IsAttached = false;
      ReadOnly = false;
      Name = name;
      PropertyType = valueType;
      CanAttachToOwner = typeof(IRenderAttachable).IsAssignableFrom(valueType);
      WireOwnerAttachment();
      OwnerType = ownerType;
      ValidateValueCallBack = validateValueCallBack;

      CheckType(valueType, metadata, name);

      AddDefaultMetadata(ownerType, metadata);

      PropertyId = nextPropertyId++;
   }

   private static void CheckType(Type valueType, PropertyMetadata metadata, String name)
   {
      // Nullable<T> (e.g. ToggleButton.IsChecked is bool?): null is a legal default, and a non-null default is BOXED as
      // its underlying T (a boxed Nullable<T> is indistinguishable from a boxed T), so validate against the underlying.
      var underlyingType = Nullable.GetUnderlyingType(valueType);
      if (underlyingType != null)
      {
         if (metadata.DefaultValue != null && !FindType(underlyingType, metadata.DefaultValue.GetType()))
            throw new ArgumentException(
               "Default value is not of the same type as property type for PropertyName " + name);
         return;
      }

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
      var property = new AdamantiumProperty(name, propertyType, ownerType);

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

   public static AdamantiumProperty RegisterAttached<T>(String name, Type ownerType)
   {
      return RegisterAttached(name, typeof(T), ownerType, new PropertyMetadata(null));
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
   
   public static AdamantiumProperty RegisterReadOnly(String name, Type propertyType, Type ownerType)
   {
      AdamantiumProperty property = new AdamantiumProperty(name, propertyType, ownerType)
      {
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