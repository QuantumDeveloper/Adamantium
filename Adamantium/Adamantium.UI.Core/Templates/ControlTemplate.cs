using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Markup;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Core.Templates;

public class ControlTemplate : UiTemplate
{
    public Type TargetType { get; set; }
    
    public TriggerCollection Triggers { get; set; }
    
    public TemplateOverridesCollection Overrides { get; set; }
    
    public override TemplateResult Build()
    {
        Content = new UIComponentFactory(Container);
        return Content.Build();
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
        var template = new ControlTemplate
        {
            Container = templateContainer
        };
        return template;
    }
}