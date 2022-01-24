using System;
using System.Collections.Generic;

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
}

public class UIComponentFactory
{
    private Dictionary<AdamantiumProperty, Object> properties;
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
}