using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Core;

public interface IControl : IInputComponent
{
    ControlTemplate Template { get; set; }

    void OnApplyTemplate();
    
    void OnRemoveTemplate();

    IAdamantiumComponent GetTemplateChild(string name);
}