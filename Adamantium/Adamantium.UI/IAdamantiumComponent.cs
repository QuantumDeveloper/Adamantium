using System;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.UI.Controls;
using Adamantium.UI.Resources;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

/// <summary>
/// Interface for getting/setting <see cref="AdamantiumProperty"/> values on an object.
/// </summary>
public interface IAdamantiumComponent : IComponent
{
    /// <summary>
    /// The parent object that inherited values are inherited from.
    /// </summary>
    IAdamantiumComponent InheritanceParent { get; }

    /// <summary>
    /// Clear <see cref="AdamantiumProperty"/>`s local value
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="priority">Priority for value</param>
    void ClearValue(string propertyName, ValuePriority priority = ValuePriority.Local);
    
    /// <summary>
    /// Clear <see cref="AdamantiumProperty"/>`s local value
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="priority">Priority for value</param>
    void ClearValue(AdamantiumProperty property, ValuePriority priority = ValuePriority.Local);

    /// <summary>
    /// Gets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="priority">Priority for value</param>
    /// <returns>The value.</returns>
    object GetValue(AdamantiumProperty property);

    /// <summary>
    /// Gets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="property">The property.</param>
    /// <param name="priority">Priority for value</param>
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
    /// <param name="priority">Priority for value</param>
    void SetValue(AdamantiumProperty property, object value, ValuePriority priority = ValuePriority.Local);

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="propertyName">Name of the AdamantiumProperty reference</param>
    /// <param name="value">The value.</param>
    /// <param name="priority">Priority for value</param>
    void SetValue(string propertyName, object value, ValuePriority priority = ValuePriority.Local);

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">New value.</param>
    void SetEffectiveValue(AdamantiumProperty property, object value);

    public void SetStyleValue(string propertyName, object value, Style style);
    
    public void SetStyleValue(AdamantiumProperty property, object value, Style style);

    public void RemoveStyleValue(string propertyName, Style style);

    /// <summary>
    /// Fires when value on <see cref="AdamantiumProperty"/> was changed
    /// </summary>
    public event EventHandler<AdamantiumPropertyChangedEventArgs> PropertyChanged;
}