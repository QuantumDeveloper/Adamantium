using Adamantium.UI.Data;

namespace Adamantium.UI
{
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
      public bool AffectsParentMeasure { get; set; }
      public bool AffectsParentArrange { get; set; }

      public PropertyMetadata()
      {
      }

      public PropertyMetadata(object defaultValue)
      {
         DefaultValue = defaultValue;
      }

      public PropertyMetadata(object defaultValue, PropertyChangedCallback propertyChangedCallback)
      {
         DefaultValue = defaultValue;
         PropertyChangedCallback = propertyChangedCallback;
      }

      public PropertyMetadata(object defaultValue, PropertyMetadataOptions options)
      {
         DefaultValue = defaultValue;
         MetadataOptions = options;
         ParseMetadataOptions(options);
      }

      public PropertyMetadata(object defaultValue, PropertyChangedCallback propertyChangedCallback, CoerceValueCallback coerceValueCallback)
      {
         DefaultValue = defaultValue;
         PropertyChangedCallback = propertyChangedCallback;
         CoerceValueCallback = coerceValueCallback;
      }

      public PropertyMetadata(object defaultValue, PropertyMetadataOptions options, PropertyChangedCallback propertyChangedCallback)
      {
         DefaultValue = defaultValue;
         PropertyChangedCallback = propertyChangedCallback;
         MetadataOptions = options;
         ParseMetadataOptions(options);
      }

      public PropertyMetadata(object defaultValue, PropertyMetadataOptions options, PropertyChangedCallback propertyChangedCallback, CoerceValueCallback coerceValueCallback)
      {
         DefaultValue = defaultValue;
         PropertyChangedCallback = propertyChangedCallback;
         CoerceValueCallback = coerceValueCallback;
         MetadataOptions = options;
         ParseMetadataOptions(options);
      }

      public PropertyMetadata(object defaultValue, PropertyMetadataOptions options, PropertyChangedCallback propertyChangedCallback, CoerceValueCallback coerceValueCallback, UpdateSourceTrigger defaultUpdateSourceTrigger)
      {
         DefaultValue = defaultValue;
         PropertyChangedCallback = propertyChangedCallback;
         CoerceValueCallback = coerceValueCallback;
         MetadataOptions = options;
         ParseMetadataOptions(options);
         DefaultUpdateSourceTrigger = defaultUpdateSourceTrigger;
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
         get
         {
            return defaultValue;
         }
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
         get
         {
            return propertyChangedCallback;
         }
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
}
