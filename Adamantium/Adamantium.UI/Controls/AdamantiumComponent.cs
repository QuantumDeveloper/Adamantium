using System;
using System.Collections.Generic;
using Adamantium.UI.Resources;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public abstract class AdamantiumComponent : DispatcherComponent, IAdamantiumComponent
{
    public UInt128 Uid { get; set; }

    private readonly Dictionary<AdamantiumProperty, ValueContainer> values = new Dictionary<AdamantiumProperty, ValueContainer>();

    private readonly Dictionary<string, StyleValueContainer> styleValues =
        new Dictionary<string, StyleValueContainer>();

    private AdamantiumComponent inheritanceParent;

    protected AdamantiumComponent()
    {
        var list = AdamantiumPropertyMap.GetRegistered(this);

        foreach (var property in list)
        {
            var metadata = property.GetDefaultMetadata(GetType());
            if (property.ValidateValueCallBack?.Invoke(metadata.DefaultValue) == false)
            {
                throw new ArgumentException($"Value {metadata} is incorrect!");
            }

            if (metadata.CoerceValueCallback != null)
            {
                var coercedValue = metadata.CoerceValueCallback?.Invoke(this, metadata.DefaultValue);
                if (coercedValue != metadata)
                {
                    if (property.ValidateValueCallBack?.Invoke(coercedValue) == false)
                    {
                        throw new ArgumentException($"Value {coercedValue} is incorrect!");
                    }

                    metadata.DefaultValue = coercedValue;
                }
            }

            if (!values.ContainsKey(property))
            {
                values[property] = new ValueContainer();
            }

            values[property].SetValue(metadata.DefaultValue, ValuePriority.Default);
            if (metadata.DefaultValue != null)
            {
                var e = new AdamantiumPropertyChangedEventArgs(property, AdamantiumProperty.UnsetValue,
                    metadata.DefaultValue);
                metadata.PropertyChangedCallback?.Invoke(this, e);
            }
        }
    }

    /// <summary>
    /// Gets the object that inherited <see cref="AdamantiumProperty"/> values are inherited from.
    /// </summary>
    IAdamantiumComponent IAdamantiumComponent.InheritanceParent => InheritanceParent;

    /// <summary>
    /// Fires when value on <see cref="AdamantiumProperty"/> was changed
    /// </summary>
    public event EventHandler<AdamantiumPropertyChangedEventArgs> PropertyChanged;

    /// <summary>
    /// Fires when some <see cref="AdamantiumProperty"/> was updated to 
    /// </summary>
    public event EventHandler<ComponentUpdatedEventArgs> ComponentUpdated;

    /// <summary>
    /// Called when <see cref="AdamantiumProperty"/> changes on the object.
    /// </summary>
    /// <param name="e"></param>
    protected virtual void OnPropertyChanged(AdamantiumPropertyChangedEventArgs e)
    {
    }

    protected virtual void OnComponentUpdated()
    {
        
    }

    protected void RaiseComponentUpdated()
    {
        OnComponentUpdated();
        ComponentUpdated?.Invoke(this, new ComponentUpdatedEventArgs(this));
    }

    protected void RaisePropertyChanged(AdamantiumProperty property, object oldValue, object newValue)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        var e = new AdamantiumPropertyChangedEventArgs(property, oldValue, newValue);

        try
        {
            OnPropertyChanged(e);
            property.OnChanged(e);
            
            PropertyChanged?.Invoke(this, e);
            
            RaiseComponentUpdated();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    /// <summary>
    /// Called when a property is changed on the current <see cref="InheritanceParent"/>.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event args.</param>
    /// <remarks>
    /// Checks for changes in an inherited property value.
    /// </remarks>
    private void ParentPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == null)
        {
            throw new ArgumentException("e.Property cannot be null");
        }

        var metadata = e.Property.GetDefaultMetadata(GetType());
        if (metadata == null) return;
        
        if (metadata.Inherits && !IsSet(e.Property))
        {
            RaisePropertyChanged(e.Property, e.OldValue, e.NewValue);
        }
    }

    /// <summary>
    /// Gets or sets the parent object that inherited <see cref="AdamantiumProperty"/> values
    /// are inherited from.
    /// </summary>
    /// <value>
    /// The inheritance parent.
    /// </value>
    public AdamantiumComponent InheritanceParent
    {
        get => inheritanceParent;
        set
        {
            if (inheritanceParent != value)
            {
                if (inheritanceParent != null)
                {
                    inheritanceParent.PropertyChanged -= ParentPropertyChanged;
                }

                //TODO: Dont forget to get all inherited properties for new object and raise property changed events for them

                inheritanceParent = value;

                if (inheritanceParent != null)
                {
                    inheritanceParent.PropertyChanged += ParentPropertyChanged;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the value of a <see cref="AdamantiumProperty"/>
    /// </summary>
    /// <param name="property"><see cref="AdamantiumProperty"/></param>
    public object this[AdamantiumProperty property]
    {
        get => GetValue(property);
        set => SetValue(property, value);
    }

    /// <summary>
    /// Gets the default value for a property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The default value.</returns>
    private object GetDefaultValue(AdamantiumProperty property)
    {
        var value = property.GetDefaultMetadata(GetType());
        if (value.Inherits && inheritanceParent != null)
        {
            return inheritanceParent.GetValue(property);
        }

        return value.DefaultValue;
    }

    public void ClearValue(string propertyName, ValuePriority priority = ValuePriority.Local)
    {
        var property = AdamantiumPropertyMap.FindRegistered(GetType(), propertyName);
        if (property == null) return;
        
        ClearValue(property, priority);
    }

    /// <summary>
    /// Clears a <see cref="AdamantiumProperty"/>'s local value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="priority"></param>
    public void ClearValue(AdamantiumProperty property, ValuePriority priority = ValuePriority.Local)
    {
        ArgumentNullException.ThrowIfNull(property);

        SetValue(property, AdamantiumProperty.UnsetValue, priority);
    }

    /// <summary>
    /// Gets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The value.</returns>
    public object GetValue(AdamantiumProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        
        object result;
        if (!AdamantiumPropertyMap.IsRegistered(this, property))
        {
            ThrowNotRegistered(property);
        }

        lock (values)
        {
            result = GetOrCalculateEffectiveValue(property);
        }

        if (result == AdamantiumProperty.UnsetValue)
        {
            result = GetDefaultValue(property);
        }

        return result;
    }

    /// <summary>
    /// Gets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="property">The property.</param>
    /// <returns>The value.</returns>
    public T GetValue<T>(AdamantiumProperty property)
    {
        return (T)GetValue(property);
    }

    /// <summary>
    /// Check if <see cref="AdamantiumProperty"/> is registered.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>True if property registered, otherwise - false</returns>
    public bool IsRegistered(AdamantiumProperty property)
    {
        return AdamantiumPropertyMap.IsRegistered(this, property);
    }

    /// <summary>
    /// Checks whether a <see cref="AdamantiumProperty"/> is set on this object.
    /// </summary>
    /// <param name="property">Adamantium property.</param>
    /// <returns>True if the property is set, otherwise false.</returns>
    public bool IsSet(AdamantiumProperty property)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        lock (values)
        {
            if (values.TryGetValue(property, out var value))
            {
                return value != AdamantiumProperty.UnsetValue;
            }
        }

        return false;
    }

    public void SetStyleValue(string propertyName, object value, Style style)
    {
        var property = AdamantiumPropertyMap.FindRegistered(GetType(), propertyName);
        SetStyleValue(property, value, style);
    }

    public void SetStyleValue(AdamantiumProperty property, object value, Style style)
    {
        SetValue(property, value, ValuePriority.Style);
        AddStyleEntry(property.Name, value, style);
    }

    public void RemoveStyleValue(string propertyName, Style style)
    {
        var previousValue = RemoveStyleEntry(propertyName, style);
        SetValue(propertyName, previousValue, ValuePriority.Style);
    }

    private void AddStyleEntry(string propertyName, object value, Style style)
    {
        if (!styleValues.ContainsKey(propertyName))
        {
            styleValues[propertyName] = new StyleValueContainer();
        }
        styleValues[propertyName].AddValue(style, value);
    }
    
    private object RemoveStyleEntry(string propertyName, Style style)
    {
        if (!styleValues.ContainsKey(propertyName))
        {
            return AdamantiumProperty.UnsetValue;
        }
        
        var previousValue = styleValues[propertyName].RemoveAndGetPreviousValue(style);
        
        return previousValue;
    }

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">New value.</param>
    /// <param name="priority"></param>
    public void SetValue(AdamantiumProperty property, object value, ValuePriority priority = ValuePriority.Local)
    {
        ValidateProperty(property);

        if (value == AdamantiumProperty.UnsetValue)
        {
            return;
        }

        lock (values)
        {
            if (property.IsAttached)
            {
                if (!values.ContainsKey(property))
                {
                    values.Add(property, new ValueContainer());
                }
                values[property].SetValue(value, priority);
            }

            if (values.ContainsKey(property))
            {
                RunSetValueSequence(property, value, priority, true);
            }
        }
    }

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">Name of the AdamantiumProperty reference</param>
    /// <param name="value">The value.</param>
    /// <param name="priority">Priority for value</param>
    public void SetValue(string property, object value, ValuePriority priority = ValuePriority.Local)
    {
        if (string.IsNullOrEmpty(property)) return;
        
        var adamantiumProperty = AdamantiumPropertyMap.FindRegistered(GetType(), property);
        if (adamantiumProperty != null)
        {
            SetValue(adamantiumProperty, value);
        }
    }

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">New value.</param>
    public void SetEffectiveValue(AdamantiumProperty property, object value)
    {
        ValidateProperty(property);

        if (value == AdamantiumProperty.UnsetValue)
        {
            return;
        }

        lock (values)
        {
            if (!values.ContainsKey(property))
            {
                return;
            }

            RunSetValueSequence(property, value, ValuePriority.Effective, false);
        }
    }

    private object GetOrCalculateEffectiveValue(AdamantiumProperty property)
    {
        if (!values.ContainsKey(property)) return AdamantiumProperty.UnsetValue;
        
        var value = values[property].GetValue(ValuePriority.Effective);
        
        if (value != AdamantiumProperty.UnsetValue) return value;
        
        foreach (var val in values[property].Values)
        {
            if (val == AdamantiumProperty.UnsetValue) continue;
            value = val;
            break;
        }

        return value;
    }

    private void RunSetValueSequence(AdamantiumProperty property, object value, ValuePriority priority, bool raiseValueChangedEvent)
    {
        var propertyValue = GetOrCalculateEffectiveValue(property);

        if (Equals(propertyValue, value))
        {
            return;
        }

        var metadata = property.GetDefaultMetadata(GetType());
        if (property.ValidateValueCallBack?.Invoke(value) == false)
        {
            throw new ArgumentException($"Value {value} is incorrect!");
        }

        if (metadata.CoerceValueCallback != null)
        {
            var newValue = metadata.CoerceValueCallback?.Invoke(this, value);
            if (newValue != value)
            {
                if (property.ValidateValueCallBack?.Invoke(value) == false)
                {
                    throw new ArgumentException($"Value {value} is incorrect!");
                }
            }

            value = newValue;
        }

        var args = new AdamantiumPropertyChangedEventArgs(property, values[property].GetValue(priority), value);
        
        if (!values.ContainsKey(property))
        {
            values.Add(property, new ValueContainer());
        }
        values[property].SetValue(value, priority);
        metadata.PropertyChangedCallback?.Invoke(this, args);
        var element = this as IUIComponent;
        if (element is IMeasurableComponent measurable)
        {
            // TODO: think how to improve this code and get rid of type casting
            if (metadata.AffectsMeasure)
            {
                measurable.InvalidateMeasure();
                measurable.InvalidateArrange();
            }
            else if (metadata.AffectsArrange)
            {
                measurable.InvalidateArrange();
            }
        }

        if (metadata.AffectsRender)
        {
            element?.InvalidateRender(false);
        }

        if (raiseValueChangedEvent)
        {
            RaisePropertyChanged(property, args.OldValue, args.NewValue);
        }
    }

    /// <summary>
    /// Throws an exception indicating that the specified property is not registered on this
    /// object.
    /// </summary>
    /// <param name="p">The property</param>
    private void ThrowNotRegistered(AdamantiumProperty p)
    {
        throw new ArgumentException($"Property '{p.Name} not registered on '{this.GetType()}");
    }

    private void ValidateProperty(AdamantiumProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (!AdamantiumPropertyMap.IsRegistered(this, property))
        {
            ThrowNotRegistered(property);
        }
    }
}