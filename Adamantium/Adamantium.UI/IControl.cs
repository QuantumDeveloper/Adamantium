namespace Adamantium.UI;

public interface IControl : IFrameworkComponent
{
    ControlTemplate Template { get; set; }

    void OnApplyTemplate();
    
    void OnRemoveTemplate();

    IAdamantiumComponent GetTemplateChild(string name);
}