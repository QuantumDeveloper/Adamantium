namespace Adamantium.UI.Core.Templates;

public class TemplateOverride : UiTemplate
{
    public string TemplatePart { get; set; }

    public override TemplateResult Build()
    {
        return Content.Build();
    }
}