using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Extensions;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;

namespace Adamantium.UI.Core.Templates;

public class TemplateResult
{
    private readonly Dictionary<string, IAdamantiumComponent> namesMap;
    private readonly List<TemplateBindingExpression> templateBindings;
    private readonly List<BindingExpression> bindingExpressions;

    internal IReadOnlyList<TemplateBindingExpression> TemplateBindings => templateBindings;

    internal IReadOnlyList<BindingExpressionBase> Bindings => bindingExpressions;
    
    internal readonly List<ITriggerActivator> Activators = [];
    
    public Guid Id { get; }

    public TemplateResult()
    {
        Id = Guid.NewGuid();
        namesMap = new Dictionary<string, IAdamantiumComponent>();
        Triggers = new TriggerCollection();
        templateBindings = new List<TemplateBindingExpression>();
        bindingExpressions = new List<BindingExpression>();
    }
    
    public IUIComponent RootComponent { get; set; }
    
    public IUIComponent HostComponent { get; set; }

    public void RegisterName(string name, IUIComponent component)
    {
        namesMap[name] = component;
    }
    
    public void AddTemplateBinding(IUIComponent target, string targetProperty, TemplateBinding binding)
    {
        templateBindings.Add(new TemplateBindingExpression(null, target, targetProperty, binding));
    }

    public void AddBinding(IUIComponent target, string sourceProperty, BindingBase binding)
    {
        bindingExpressions.Add(new BindingExpression(target, sourceProperty, binding));
    }

    public IAdamantiumComponent GetComponentByName(string name)
    {
        namesMap.TryGetValue(name, out var component);

        return component;
    }
    
    public TriggerCollection Triggers { get; }

    public void Destroy()
    {
        RootComponent.TraverseVisualTree(component =>
        {
            var fundamental = (FundamentalUIComponent)component;
            fundamental.TemplatedParent = null;
        });

        foreach (var binding in TemplateBindings)
        {
            binding.CloseConnection();
        }

        foreach (var binding in bindingExpressions)
        {
            binding.CloseConnection();
        }

        if (Triggers.Count > 0)
        {
            foreach (var activator in Activators)
            {
                activator.Deactivate();
            }
        }
    }
}