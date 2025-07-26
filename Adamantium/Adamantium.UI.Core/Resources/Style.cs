using Adamantium.UI.Core.Resources.Triggers;

namespace Adamantium.UI.Core.Resources;

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

    public Selector Selector { get; set; }
    
    public SetterCollection Setters { get; }

    public TriggerCollection Triggers { get; }

    public void Add(object child)
    {
        switch (child)
        {
            case Setter setter:
                Setters.Add(setter);
                break;
            case ITrigger trigger:
                Triggers.Add(trigger);
                break;
            default:
                throw new InvalidOperationException(
                    $"Type '{child?.GetType().FullName}' cannot be added to a Style."
                );
        }
    }

    public static void Apply(IFundamentalUIComponent component, params Style[] styles)
    {
        if (styles == null) return;
        
        foreach (var style in styles)
        {
            style.Attach(component);
        }
    }
    
    public static void UnApply(IFundamentalUIComponent component, params Style[] styles)
    {
        if (styles == null) return;
        
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
            setter.Apply(component, this, Theme);
        }

        foreach (var trigger in Triggers)
        {
            trigger.Apply(component, Theme);
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
            setter.Remove(component, this, Theme);
        }

        foreach (var trigger in Triggers)
        {
            trigger.Remove(component);
        }
    }
}