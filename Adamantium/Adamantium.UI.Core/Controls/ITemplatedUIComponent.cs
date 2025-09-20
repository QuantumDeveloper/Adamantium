using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Core.Controls;

public interface ITemplatedUIComponent : IInputComponent
{
    ControlTemplate Template { get; set; }
    
    void OnApplyTemplate();
    
    void OnRemoveTemplate();

    IAdamantiumComponent GetTemplateChild(string name);
}