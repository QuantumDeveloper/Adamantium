using System;
using System.Collections.Generic;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Resources;

public class Style : AdamantiumComponent
{
    private Dictionary<AdamantiumProperty, ISetter> settersDict;

    public Style()
    {
        settersDict = new Dictionary<AdamantiumProperty, ISetter>();
        Setters = new SetterCollection();
        Triggers = new TriggerCollection();
        Selector = new Selector();
    }

    internal ITheme Theme { get; set; }

    public required Selector Selector { get; set; }

    [Content] public SetterCollection Setters { get; }

    public TriggerCollection Triggers { get; }

    public static void Apply(IFundamentalUIComponent component, params Style[] styles)
    {
        foreach (var style in styles)
        {
            style.Attach(component);
        }
    }
    
    public static void UnApply(IFundamentalUIComponent component, params Style[] styles)
    {
        foreach (var style in styles)
        {
            style.Detach(component);
        }
    }

    public void Attach(IFundamentalUIComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (!Selector.Match(component))
        {
            return;
        }

        foreach (var setter in Setters)
        {
            setter.Apply(component, Theme);
        }

        foreach (var trigger in Triggers)
        {
            trigger.Apply(component);
        }
    }
    
    public void Detach(IFundamentalUIComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (!Selector.Match(component))
        {
            return;
        }

        foreach (var setter in Setters)
        {
            setter.Apply(component, Theme);
        }

        foreach (var trigger in Triggers)
        {
            trigger.Apply(component);
        }
    }
}