using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.UI.Controls;

namespace Adamantium.UI;

public abstract class UITemplate : DispatcherComponent
{
    public UIComponentFactory Content { get; set; }

    public abstract TemplateResult Build();
}

public class ControlTemplate : UITemplate
{
    public Type TargetType { get; set; }
    
    public TriggerCollection Triggers { get; set; }
    public override TemplateResult Build()
    {
        return Content.Build();
    }
}

public class NameScope : INameScope
{
    private readonly Dictionary<string, IUIComponent> nameScope;

    public NameScope()
    {
        nameScope = new Dictionary<string, IUIComponent>();
    }
    
    public void RegisterName(string name, IUIComponent component)
    {
        if (string.IsNullOrEmpty(name)) return;

        if (nameScope.TryGetValue(name, out var existingComponent))
        {
            if (component != existingComponent)
                throw new ArgumentException($"Component with name {name} already registered");
        }
        else
        {
            nameScope[name] = component;
        }
        
    }

    public void Unregister(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        
        nameScope.Remove(name);
    }

    public IUIComponent Find(string name)
    {
        nameScope.TryGetValue(name, out var component);
        return component;
    }
}

public class TemplateResult
{
    private Dictionary<string, IUIComponent> namesMap;

    public TemplateResult()
    {
        namesMap = new Dictionary<string, IUIComponent>();
    }
    
    public IUIComponent RootComponent { get; internal set; }

    internal void RegisterName(string name, IUIComponent component)
    {
        namesMap[name] = component;
    }

    public IUIComponent GetComponentByName(string name)
    {
        namesMap.TryGetValue(name, out var component);

        return component;
    }
}

public class UIComponentFactory
{
    private readonly Dictionary<AdamantiumProperty, Object> properties;
    private readonly List<UIComponentFactory> children;

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
    
    public TemplateResult Build()
    {
        var componentsMap = new Dictionary<UIComponentFactory, IUIComponent>();
        var stack = new Stack<UIComponentFactory>();
        stack.Push(this);

        var result = new TemplateResult();
        IUIComponent rootComponent = null;
        while (stack.Count > 0)
        {
            var factory = stack.Pop();
            
            var component = factory.GenerateComponent();
            if (!string.IsNullOrEmpty(factory.Name))
            {
                rootComponent ??= component;
                result.RegisterName(Name, component);
                result.RootComponent = rootComponent;
            }
            
            componentsMap[factory] = component;
            if (factory.Parent != null)
            {
                if (componentsMap.TryGetValue(factory.Parent, out var uiComponent) && uiComponent is IContainer container)
                {
                    container.AddOrSetChildComponent(component);
                }
            }

            foreach (var childFactory in factory.children)
            {
                stack.Push(childFactory);
            }
        }
        
        return result;
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

        foreach (var (property, value) in properties)
        {
            component.SetValue(property, value);
        }
        
        return component;
    }
}