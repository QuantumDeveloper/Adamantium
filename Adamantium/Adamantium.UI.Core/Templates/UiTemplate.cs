namespace Adamantium.UI.Core.Templates;

public abstract class UiTemplate
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

    public void SetBindingInstruction()
    {
        
    }
}