namespace Adamantium.UI
{
   /// <summary>
   /// Interface for getting/setting <see cref="AdamantiumProperty"/> values on an object.
   /// </summary>
   public interface IAdamantiumObject
   {
      /// <summary>
      /// The parent object that inherited values are inherited from.
      /// </summary>
      IAdamantiumObject InheritanceParent { get; }


      /// <summary>
      /// Clear <see cref="AdamantiumProperty"/>`s local value
      /// </summary>
      /// <param name="property"></param>
      void ClearValue(AdamantiumProperty property);

      /// <summary>
      /// Gets a <see cref="AdamantiumProperty"/> value.
      /// </summary>
      /// <param name="property">The property.</param>
      /// <returns>The value.</returns>
      object GetValue(AdamantiumProperty property);

      /// <summary>
      /// Gets a <see cref="AdamantiumProperty"/> value.
      /// </summary>
      /// <typeparam name="T">The type of the property.</typeparam>
      /// <param name="property">The property.</param>
      /// <returns>The value.</returns>
      T GetValue<T>(AdamantiumProperty property);

      /// <summary>
      /// Checks whether a <see cref="AdamantiumProperty"/> is registered on this object.
      /// </summary>
      /// <param name="property">The property.</param>
      /// <returns>True if the property is registered, otherwise false.</returns>
      bool IsRegistered(AdamantiumProperty property);

      /// <summary>
      /// Checks whether a <see cref="AdamantiumProperty"/> is set on this object.
      /// </summary>
      /// <param name="property">The property.</param>
      /// <returns>True if the property is set, otherwise false.</returns>
      bool IsSet(AdamantiumProperty property);

      /// <summary>
      /// Sets a <see cref="AdamantiumProperty"/> value.
      /// </summary>
      /// <param name="property">The property.</param>
      /// <param name="value">The value.</param>
      void SetValue(AdamantiumProperty property, object value);

      
   }
}
