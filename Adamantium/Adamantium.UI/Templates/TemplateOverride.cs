namespace Adamantium.UI.Templates;

public class TemplateOverride : UiTemplate
{
    public string TemplatePart { get; set; }

    public override TemplateResult Build()
    {
        return Content.Build();
    }
}