using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.Templates;

public abstract class UiTemplate
{
    internal AumlTemplateContainer Container { get; set; }
    
    public UIComponentFactory Content { get; set; }
    
    public TriggerCollection Triggers { get; set; }

    public abstract TemplateResult Build(IUIComponent templatedParent);
}