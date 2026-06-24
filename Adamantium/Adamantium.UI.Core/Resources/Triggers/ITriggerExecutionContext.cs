using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITriggerExecutionContext
{
    IFundamentalUIComponent HostComponent { get; }
    ITheme Theme { get; }
    IAdamantiumComponent FindTarget(string targetName);

}

internal class StyleTriggerExecutionContext : ITriggerExecutionContext
{
    public StyleTriggerExecutionContext(IFundamentalUIComponent host, ITheme theme)
    {
        HostComponent = host;
        Theme = theme;
    }

    public IFundamentalUIComponent HostComponent { get; set; }
    public ITheme Theme { get; set; }

    public IAdamantiumComponent FindTarget(string targetName)
    {
        if (string.IsNullOrEmpty(targetName)) return HostComponent;

        // A style-level trigger may target a NAMED PART of the host's template - this is what lets part triggers live in
        // their own <Style> instead of only inside ControlTemplate.Triggers. Resolved lazily against the host's current
        // template: a state trigger only needs the part when it fires, by which point the template (applied by another
        // style's Template setter) exists, regardless of the order the styles were attached.
        return (HostComponent as ITemplatedUIComponent)?.GetTemplateChild(targetName);
    }
}

internal class TemplateTriggerExecutionContext : ITriggerExecutionContext
{
    private readonly TemplateResult _templateResult;
    
    public TemplateTriggerExecutionContext(IUIComponent host, ITheme theme, TemplateResult templateResult)
    {
        HostComponent = host;
        Theme = theme;
        _templateResult = templateResult;
    }
    
    public IFundamentalUIComponent HostComponent { get; set; }
    public ITheme Theme { get; set; }

    public IAdamantiumComponent FindTarget(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            return HostComponent;
        }
        
        
        return _templateResult.GetComponentByName(targetName);
    }
}