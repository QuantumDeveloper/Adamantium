using System;
using System.Collections.Generic;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public abstract class AdamantiumComponent : DispatcherComponent, IAdamantiumComponent
{
    public long Uid { get; set; }

    private readonly Dictionary<AdamantiumProperty, object> values = new Dictionary<AdamantiumProperty, object>();

    private AdamantiumComponent inheritanceParent;

    protected AdamantiumComponent()
    {
        var list = AdamantiumPropertyMap.GetRegistered(this);

        foreach (var property in list)
        {
            var metadata = property.GetDefaultMetadata(GetType());
            if (property.ValidateValueCallBack?.Invoke(metadata.DefaultValue) == false)
            {
                throw new ArgumentException("Value " + metadata + "is incorrect!");
            }

            if (metadata.CoerceValueCallback != null)
            {
                var coercedValue = metadata.CoerceValueCallback?.Invoke(this, metadata.DefaultValue);
                if (coercedValue != metadata)
                {
                    if (property.ValidateValueCallBack?.Invoke(coercedValue) == false)
                    {
                        throw new ArgumentException("Value " + coercedValue + "is incorrect!");
                    }

                    metadata.DefaultValue = coercedValue;
                }
            }

            values[property] = metadata.DefaultValue;
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
        if (metadata != null)
        {
            if (metadata.Inherits && !IsSet(e.Property))
            {
                RaisePropertyChanged(e.Property, e.OldValue, e.NewValue);
            }
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
        else
        {
            return value.DefaultValue;
        }
    }

    /// <summary>
    /// Clears a <see cref="AdamantiumProperty"/>'s local value.
    /// </summary>
    /// <param name="property">The property.</param>
    public void ClearValue(AdamantiumProperty property)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        SetValue(property, AdamantiumProperty.UnsetValue);
    }

    /// <summary>
    /// Gets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The value.</returns>
    public object GetValue(AdamantiumProperty property)
    {
        object result = AdamantiumProperty.UnsetValue;
        if (!AdamantiumPropertyMap.IsRegistered(this, property))
        {
            ThrowNotRegistered(property);
        }

        lock (values)
        {
            if (values.ContainsKey(property))
            {
                result = values[property];
            }
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
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

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
            if (values.ContainsKey(property))
            {
                return values[property] != AdamantiumProperty.UnsetValue;
            }
        }

        return false;
    }

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">New value.</param>
    public void SetValue(AdamantiumProperty property, object value)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        if (!AdamantiumPropertyMap.IsRegistered(this, property))
        {
            ThrowNotRegistered(property);
        }

        if (value == AdamantiumProperty.UnsetValue)
        {
            return;
        }

        lock (values)
        {
            if (property.IsAttached && !values.ContainsKey(property))
            {
                values.Add(property, value);
            }

            if (values.ContainsKey(property))
            {
                RunSetValueSequence(property, value, true);
            }
        }
    }

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">New value.</param>
    public void SetCurrentValue(AdamantiumProperty property, object value)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        if (!AdamantiumPropertyMap.IsRegistered(this, property))
        {
            ThrowNotRegistered(property);
        }

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

            RunSetValueSequence(property, value, false);
        }
    }

    private void RunSetValueSequence(AdamantiumProperty property, object value, bool raiseValueChangedEvent)
    {
        var propertyValue = values[property];

        if (Equals(propertyValue, value))
        {
            return;
        }

        var metadata = property.GetDefaultMetadata(GetType());
        if (property.ValidateValueCallBack?.Invoke(value) == false)
        {
            throw new ArgumentException("Value " + value + "is incorrect!");
        }

        if (metadata.CoerceValueCallback != null)
        {
            var newValue = metadata.CoerceValueCallback?.Invoke(this, value);
            if (newValue != value)
            {
                if (property.ValidateValueCallBack?.Invoke(value) == false)
                {
                    throw new ArgumentException("Value " + value + "is incorrect!");
                }
            }

            value = newValue;
        }

        var args = new AdamantiumPropertyChangedEventArgs(property, values[property], value);
        values[property] = value;
        metadata.PropertyChangedCallback?.Invoke(this, args);
        var element = this as IUIComponent;
        if (metadata.AffectsMeasure && element is IMeasurableComponent measurable)
        {
            measurable?.InvalidateMeasure();
        }

        if (metadata.AffectsArrange  && element is IMeasurableComponent measurable1)
        {
            measurable1?.InvalidateArrange();
        }

        if (metadata.AffectsRender)
        {
            element?.InvalidateRender();
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

}