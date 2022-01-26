using System;
using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.UI;

public abstract class UITemplate
{
    public UIComponentFactory Content { get; set; }
}

public class ControlTemplate : UITemplate
{
    public IAdamantiumComponent FindName(string name)
    {
        return null;
    }
    
    public TriggerCollection Triggers { get; set; }
}

public class UIComponentFactory
{
    private readonly Dictionary<AdamantiumProperty, Object> properties;
    private List<UIComponentFactory> children;

    public UIComponentFactory()
    {
        properties = new Dictionary<AdamantiumProperty, object>();
        children = new List<UIComponentFactory>();
    }

    public UIComponentFactory Parent { get; set; }
    
    public String Name { get; set; }
    
    public Type Type { get; set; }
    
    public UIComponentFactory FirstChild { get; }
    
    public UIComponentFactory NextSibling { get; }

    public void AddSibling(UIComponentFactory sibling)
    {
        children.Add(sibling);
    }

    public void SetValue(AdamantiumProperty property, object value)
    {
        properties[property] = value;
    }

    public IUIComponent GenerateVisualTree()
    {
        return null;
    }


    private IUIComponent GenerateComponent()
    {
        ArgumentNullException.ThrowIfNull(Type);
        
        if (!Utilities.IsTypeInheritFrom(Type, typeof(IUIComponent)))
        {
            throw new ArgumentOutOfRangeException($"{Type} should be inherited from IUIComponent");
        }
        
        var component = (IUIComponent)Activator.CreateInstance(Type);
        
        ArgumentNullException.ThrowIfNull(component);

        var stack = new Stack<UIComponentFactory>();
        stack.Push(this);

        foreach (var (property, value) in properties)
        {
            component.SetValue(property, value);
        }
        
        
        
        return component;
    }
}