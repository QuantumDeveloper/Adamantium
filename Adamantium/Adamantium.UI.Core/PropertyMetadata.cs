using Adamantium.UI.Core.Data;

namespace Adamantium.UI.Core;

public class PropertyMetadata
{
   private object defaultValue;
   private PropertyChangedCallback propertyChangedCallback;
   private CoerceValueCallback coerceValueCallback;
   public PropertyMetadataOptions MetadataOptions { get; } = PropertyMetadataOptions.None;

   public BindingMode DefaultBindingMode { get; private set; } = BindingMode.OneWay;

   public UpdateSourceTrigger DefaultUpdateSourceTrigger { get; private set; }
      
   public bool IsDataBindingAllowed { get; private set; }
   public bool IsNotDataBindable { get; private set; }
   public bool Inherits { get; private set; }
   public bool AffectsMeasure { get; set; }
   public bool AffectsArrange { get; set; }
   public bool AffectsRender { get; set; }

   /// <summary>See <see cref="PropertyMetadataOptions.AffectsPaint"/>: this property only re-COLOURS, so it re-bakes
   /// instead of re-recording - and its animation can run on the compositor.</summary>
   public bool AffectsPaint { get; set; }

   public bool AffectsParentMeasure { get; set; }
   public bool AffectsParentArrange { get; set; }

   /// <summary>Which fields this instance actually STATED - <see cref="MergedWith"/> takes the rest from the base, so
   /// "silent" has to be distinguishable from "explicitly the default of its type".</summary>
   [Flags]
   private enum Stated
   {
      None = 0,
      DefaultValue = 1,
      Options = 2,
      PropertyChangedCallback = 4,
      CoerceValueCallback = 8,
      UpdateSourceTrigger = 16,
   }

   private readonly Stated stated;

   public PropertyMetadata()
   {
   }

   public PropertyMetadata(object defaultValue)
   {
      DefaultValue = defaultValue;
      stated = Stated.DefaultValue;
   }

   public PropertyMetadata(object defaultValue, PropertyChangedCallback propertyChangedCallback)
   {
      DefaultValue = defaultValue;
      PropertyChangedCallback = propertyChangedCallback;
      stated = Stated.DefaultValue | Stated.PropertyChangedCallback;
   }

   public PropertyMetadata(object defaultValue, PropertyMetadataOptions options)
   {
      DefaultValue = defaultValue;
      MetadataOptions = options;
      ParseMetadataOptions(options);
      stated = Stated.DefaultValue | Stated.Options;
   }

   public PropertyMetadata(object defaultValue, PropertyChangedCallback propertyChangedCallback, CoerceValueCallback coerceValueCallback)
   {
      DefaultValue = defaultValue;
      PropertyChangedCallback = propertyChangedCallback;
      CoerceValueCallback = coerceValueCallback;
      stated = Stated.DefaultValue | Stated.PropertyChangedCallback | Stated.CoerceValueCallback;
   }

   public PropertyMetadata(object defaultValue, PropertyMetadataOptions options, PropertyChangedCallback propertyChangedCallback)
   {
      DefaultValue = defaultValue;
      PropertyChangedCallback = propertyChangedCallback;
      MetadataOptions = options;
      ParseMetadataOptions(options);
      stated = Stated.DefaultValue | Stated.Options | Stated.PropertyChangedCallback;
   }

   public PropertyMetadata(object defaultValue, PropertyMetadataOptions options, CoerceValueCallback coerceValueCallback)
   {
      DefaultValue = defaultValue;
      PropertyChangedCallback = null;
      CoerceValueCallback = coerceValueCallback;
      MetadataOptions = options;
      ParseMetadataOptions(options);
      stated = Stated.DefaultValue | Stated.Options | Stated.CoerceValueCallback;
   }

   public PropertyMetadata(object defaultValue, PropertyMetadataOptions options, PropertyChangedCallback propertyChangedCallback, CoerceValueCallback coerceValueCallback)
   {
      DefaultValue = defaultValue;
      PropertyChangedCallback = propertyChangedCallback;
      CoerceValueCallback = coerceValueCallback;
      MetadataOptions = options;
      ParseMetadataOptions(options);
      stated = Stated.DefaultValue | Stated.Options | Stated.PropertyChangedCallback | Stated.CoerceValueCallback;
   }

   public PropertyMetadata(object defaultValue, PropertyMetadataOptions options, PropertyChangedCallback propertyChangedCallback, CoerceValueCallback coerceValueCallback, UpdateSourceTrigger defaultUpdateSourceTrigger)
   {
      DefaultValue = defaultValue;
      PropertyChangedCallback = propertyChangedCallback;
      CoerceValueCallback = coerceValueCallback;
      MetadataOptions = options;
      ParseMetadataOptions(options);
      DefaultUpdateSourceTrigger = defaultUpdateSourceTrigger;
      stated = Stated.DefaultValue | Stated.Options | Stated.PropertyChangedCallback | Stated.CoerceValueCallback |
               Stated.UpdateSourceTrigger;
   }

   /// <summary>This metadata laid OVER <paramref name="baseMetadata"/>: whatever it did not state comes from the base.
   /// Changed-callbacks chain base-first; coercion does not chain (two answers are not an answer) - the derived wins.</summary>
   internal PropertyMetadata MergedWith(PropertyMetadata baseMetadata)
   {
      if (baseMetadata == null) return this;

      var options = stated.HasFlag(Stated.Options) ? MetadataOptions : baseMetadata.MetadataOptions;
      var defaultValue = stated.HasFlag(Stated.DefaultValue) ? DefaultValue : baseMetadata.DefaultValue;
      var coerce = stated.HasFlag(Stated.CoerceValueCallback) ? CoerceValueCallback : baseMetadata.CoerceValueCallback;
      var trigger = stated.HasFlag(Stated.UpdateSourceTrigger)
         ? DefaultUpdateSourceTrigger
         : baseMetadata.DefaultUpdateSourceTrigger;

      return new PropertyMetadata(defaultValue, options,
         Chain(baseMetadata.PropertyChangedCallback, PropertyChangedCallback), coerce, trigger);
   }

   // base + derived, minus anything already in base: a handler restated by an override must still run once per change.
   private static PropertyChangedCallback Chain(PropertyChangedCallback baseCallback, PropertyChangedCallback derived)
   {
      if (baseCallback == null) return derived;
      if (derived == null) return baseCallback;

      var result = baseCallback;
      foreach (var handler in derived.GetInvocationList())
      {
         var one = (PropertyChangedCallback)handler;
         if (Array.IndexOf(result.GetInvocationList(), one) >= 0) continue;
         result += one;
      }

      return result;
   }

   private void ParseMetadataOptions(PropertyMetadataOptions flags)
   {
      if ((flags & PropertyMetadataOptions.Inherits) > 0)
      {
         Inherits = true;
      }
      if ((flags & PropertyMetadataOptions.NotDataBindable) > 0)
      {
         IsDataBindingAllowed = false;
         IsNotDataBindable = true;
      }
      if ((flags & PropertyMetadataOptions.AffectsMeasure) > 0)
      {
         AffectsMeasure = true;
      }
      if ((flags & PropertyMetadataOptions.AffectsArrange) > 0)
      {
         AffectsArrange = true;
      }
      if ((flags & PropertyMetadataOptions.AffectsRender) > 0)
      {
         AffectsRender = true;
      }
      if ((flags & PropertyMetadataOptions.AffectsPaint) > 0)
      {
         AffectsPaint = true;
      }
      if ((flags & PropertyMetadataOptions.AffectsParentMeasure) > 0)
      {
         AffectsParentMeasure = true;
      }
      if ((flags & PropertyMetadataOptions.AffectsParentArrange) > 0)
      {
         AffectsParentArrange = true;
      }
      if ((flags & PropertyMetadataOptions.BindsTwoWayByDefault) > 0)
      {
         DefaultBindingMode = BindingMode.TwoWay;
      }
   }

   public object DefaultValue
   {
      get => defaultValue;
      set
      {
         if (!IsSealed)
         {
            defaultValue = value;
         }
      }
   }

   public PropertyChangedCallback PropertyChangedCallback
   {
      get => propertyChangedCallback;
      set
      {
         if (!IsSealed)
         {
            propertyChangedCallback = value;
         }
      }
   }

   public CoerceValueCallback CoerceValueCallback
   {
      get => coerceValueCallback;
      set
      {
         if (!IsSealed)
         {
            coerceValueCallback = value;
         }
      }
   }

   public bool IsSealed { get; internal set; }

   //protected virtual void OnApply(AdamantiumProperty ap, Type targeType)
   //{
         
   //}
}