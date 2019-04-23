using System;

namespace Adamantium.UI
{
   /// <summary>
   /// A set of flags describing <see cref="AdamantiumProperty"/>  behavior
   /// </summary>
   [Flags]
   public enum PropertyMetadataOptions
   {
      /// <summary>
      /// No options
      /// </summary>
      None = 0,
      /// <summary>
      /// If BindingMode will not set explicitly, default binding mode will set to TwoWay
      /// </summary>
      BindsTwoWayByDefault = 1,
      
      /// <summary>
      /// This property inherits value from the parent property in Logical Tree
      /// </summary>
      Inherits = 2,
      
      /// <summary>
      /// DataBinding cannot be set on particular <see cref="AdamantiumProperty"/> 
      /// </summary>
      NotDataBindable = 4,

      /// <summary>
      /// Definining this flag means that changing it will influnce on measure of current instance
      /// </summary>
      AffectsMeasure = 8,

      /// <summary>
      /// Definining this flag means that changing it will influnce on Parents measure
      /// </summary>
      AffectsParentMeasure = 16,

      /// <summary>
      /// Definining this flag means that changing it will influnce on arrange of current instance
      /// </summary>
      AffectsArrange = 32,

      /// <summary>
      /// Definining this flag means that changing it will influnce on Parents arrange
      /// </summary>
      AffectsParentArrange = 64,

      /// <summary>
      /// Defining this flag means that control geometry will be recteated if this property will changed
      /// </summary>
      AffectsRender = 128
   }
}
