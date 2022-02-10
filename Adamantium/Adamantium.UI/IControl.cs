namespace Adamantium.UI;

public interface IControl : IMeasurableComponent
{
    ControlTemplate Template { get; set; }

    void OnApplyTemplate();
    
    void OnRemoveTemplate();

    IAdamantiumComponent GetTemplateChild(string name);
}