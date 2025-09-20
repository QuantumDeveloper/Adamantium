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
    
    // In context style target could be only HostComponent
    public IAdamantiumComponent FindTarget(string targetName)
    {
        return string.IsNullOrEmpty(targetName) ? HostComponent : null;

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