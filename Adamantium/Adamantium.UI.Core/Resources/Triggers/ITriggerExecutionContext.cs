using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITriggerExecutionContext
{
    IFundamentalUIComponent HostComponent { get; }
    ITheme Theme { get; }
    IAdamantiumComponent FindTarget(string targetName);

}

internal static class TriggerTargetResolver
{
    /// <summary>Last resort for <see cref="ITriggerExecutionContext.FindTarget"/>: a KEYED RESOURCE (resolved tree-scoped
    /// from the host: Local -> Theme -> Global). A named part is per-host, but some targets are deliberately SHARED - the
    /// loading-skeleton pulse runs on ONE theme brush that every skeleton card paints with, so animating it costs one
    /// animation instead of one per card. Only an AdamantiumComponent resource (a Brush) can be a target; anything else
    /// (a colour, a double) resolves to null exactly as an unknown name does.</summary>
    public static IAdamantiumComponent FindKeyedTarget(this ITriggerExecutionContext context, string key)
        => context.Theme?.GetResource(context.HostComponent, key) as IAdamantiumComponent;
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
        return (HostComponent as ITemplatedUIComponent)?.GetTemplateChild(targetName)
               ?? this.FindKeyedTarget(targetName);
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


        return _templateResult.GetComponentByName(targetName) ?? this.FindKeyedTarget(targetName);
    }
}