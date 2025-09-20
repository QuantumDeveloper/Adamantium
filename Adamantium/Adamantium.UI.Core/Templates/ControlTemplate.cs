using Adamantium.UI.Core.Extensions;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Core.Templates;

public class ControlTemplate : UiTemplate
{
    public Type TargetType { get; set; }
    
    private readonly Func<TemplateResult> _templateBuilder;

    public ControlTemplate()
    {
        
    }
   
    public ControlTemplate(Func<TemplateResult> templateBuilder)
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

            foreach (var binding in result.TemplateBindings)
            {
                binding.Source = templatedParent;
                binding.EstablishConnection();
            }
            
            foreach (var binding in result.Bindings)
            {
                binding.EstablishConnection();
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

    public static ControlTemplate Load(string auml)
    {
        var result = AumlParser.Parse(auml);
        if (!result.HasErrors)
        {
            return Load(result);
        }
        return null;
    }
    
    public static ControlTemplate Load(AumlDocument doc)
    {
        var templateContainer = doc.TransformAumlDocument();
        var template = new ControlTemplate()
        {
            Container = templateContainer
        };
        return template;
    }
}