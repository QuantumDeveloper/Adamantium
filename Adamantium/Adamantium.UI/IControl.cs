using Adamantium.UI.Input;
using Adamantium.UI.Templates;

namespace Adamantium.UI;

public interface IControl : IInputComponent
{
    ControlTemplate Template { get; set; }

    void OnApplyTemplate();
    
    void OnRemoveTemplate();

    IAdamantiumComponent GetTemplateChild(string name);
}