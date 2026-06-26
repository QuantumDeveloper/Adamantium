using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Extensions;
using Adamantium.UI.Core.Resources.Triggers;

namespace Adamantium.UI.Core.Templates;

public class DataTemplate : UiTemplate
{
    private readonly Func<TemplateResult> _templateBuilder;

    public DataTemplate()
    {
        
    }
   
    public DataTemplate(Func<TemplateResult> templateBuilder)
    {
        _templateBuilder = templateBuilder ?? throw new ArgumentNullException(nameof(templateBuilder));
    }
    
    public override TemplateResult Build(IUIComponent templatedParent)
    {
        if (_templateBuilder != null)
        {
            var result = _templateBuilder();
            
            result.HostComponent = templatedParent;
            
            result.RootComponent.TraverseVisualTree(component =>
            {
                var fundamental = (FundamentalUIComponent)component;
                fundamental.TemplatedParent = templatedParent;
            });

            // Register through the engine (not a bare EstablishConnection): a DataTemplate's {Binding}s are built before
            // the container's DataContext is assigned, so they must re-resolve when it arrives (DataContext change ->
            // BindingEngine.RefreshBindings). This is what makes ItemTemplate/ContentTemplate bindings resolve per item.
            foreach (var binding in result.Bindings)
            {
                BindingEngine.Register(binding);
            }

            if (result.Triggers.Count > 0)
            {
                foreach (var trigger in result.Triggers)
                {
                    var context = new TemplateTriggerExecutionContext(result.RootComponent, UIAppContext.Current.ThemeManager.CurrentTheme, result);
                    var activator = trigger.Apply(context);
                    result.Activators.Add(activator);
                }
            }

            return result;
        }
        else
        {
            Content = new UIComponentFactory(Container);
            return Content.Build();
        }
    }
}