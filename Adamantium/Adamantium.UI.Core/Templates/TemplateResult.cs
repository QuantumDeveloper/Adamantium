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

    // Accepts any IAdamantiumComponent, not only IUIComponent: a template may x:Name a NON-visual component (e.g. a
    // GradientStop inside a brush) so a trigger/animation can target it. The namesMap and GetComponentByName already
    // work in IAdamantiumComponent terms; only this entry point was narrowed.
    public void RegisterName(string name, IAdamantiumComponent component)
    {
        namesMap[name] = component;
    }
    
    // Same reason as RegisterName: a template binding may target a NON-visual component (a ColumnDefinition width, a
    // GradientStop offset), and the expression has always held its target as IAdamantiumComponent.
    public void AddTemplateBinding(IAdamantiumComponent target, string targetProperty, TemplateBinding binding)
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

            // ...and let go of the values that DRAW it. Leaving the tree gives those up (UIComponent's detach), but a
            // template part that was never IN the tree never gets that call - popup content is built eagerly and lives
            // detached - so its brush keeps it, and a theme brush outlives the application. Idempotent: a part that DID
            // leave the tree already released, and releasing again is a no-op.
            fundamental.ReleaseRenderAttachments();
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