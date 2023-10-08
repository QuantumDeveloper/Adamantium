using Adamantium.UI.Controls;
using Adamantium.UI.Markup;

namespace Adamantium.UI.Templates;

public abstract class UiTemplate : DispatcherComponent
{
    private NameScope _nameScope;
    
    protected UiTemplate()
    {
        _nameScope = new NameScope();
    }
    
    internal AumlTemplateContainer Container { get; set; }
    
    public UIComponentFactory Content { get; set; }

    public abstract TemplateResult Build();

    public void RegisterName(string name, object scopedElement)
    {
        _nameScope.RegisterName(name, scopedElement);
    }

    public void UnregisterName(string name)
    {
        _nameScope.Unregister(name);
    }

    public object FindName(string name)
    {
        return _nameScope.Find(name);
    }
}