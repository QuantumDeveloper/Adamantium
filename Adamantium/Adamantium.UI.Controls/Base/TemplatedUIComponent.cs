using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls.Base;

public class TemplatedUIComponent : InputUIComponent, ITemplatedUIComponent
{
    private TemplateResult templateResult;
    private ControlTemplate appliedTemplate;

    public static readonly AdamantiumProperty TemplateProperty =
        AdamantiumProperty.Register(nameof(Template), typeof(ControlTemplate), typeof(MeasurableUIComponent),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, TemplateChangedCallback));

    private static void TemplateChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is TemplatedUIComponent component)
        {
            component.OnTemplateChanged();
        }
    }

    // The metadata callback fires on EVERY write to the property (any priority slot), not only when the EFFECTIVE value
    // changes. So a trigger that swaps Template at Trigger priority - over a Style-priority base - must (re)build off the
    // effective template, not off the raw written value: otherwise a masked lower-priority write would wrongly rebuild,
    // and swap-back would tear the template down without restoring the base. Rebuild only when the effective template
    // actually differs from the one currently applied.
    private void OnTemplateChanged()
    {
        var effective = Template;
        if (ReferenceEquals(effective, appliedTemplate)) return;

        if (appliedTemplate != null) RemoveTemplate();
        appliedTemplate = effective;
        if (effective != null) ApplyTemplate();

        // Template parts just changed: re-point any style/element triggers that target named parts at the new tree
        // (and tear down what they held on the old, now-discarded parts) so a runtime template swap stays leak-free.
        ReevaluateTriggersForTemplateChange();
    }
    
    public ControlTemplate Template
    {
        get => GetValue<ControlTemplate>(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }
    
    public IAdamantiumComponent GetTemplateChild(string name)
    {
        if (Template == null || templateResult?.RootComponent == null) return null;

        return templateResult.GetComponentByName(name);
    }

    private void ApplyTemplate()
    {
        if (Template == null) return;
      
        templateResult = Template.Build(this);
        if (templateResult != null)
        {
            // var overrides = ControlTemplateOverride.GetOverrides(this);
            // if (overrides != null)
            // {
            //     foreach (var @override in overrides)
            //     {
            //         // TODO: add here logic for applying overrides
            //     }
            // }
            
            AddTemplateChild(templateResult.RootComponent);
            OnApplyTemplate();
        }
    }

    private void RemoveTemplate()
    {
        // A prior ApplyTemplate whose Build returned null leaves templateResult null while appliedTemplate is set;
        // nothing to tear down then.
        if (templateResult == null) return;
        TraverseVisualTreeAndUnload(templateResult.RootComponent);
        RemoveVisualChildren();
        templateResult.Destroy();
        templateResult = null;
        OnRemoveTemplate();
    }
    
    protected void AddTemplateChild(IUIComponent child)
    {
        AddVisualChild(child);
    }

    public virtual void OnRemoveTemplate()
    {
        
    }
    
    private void TraverseVisualTreeAndUnload(IUIComponent component)
    {
        foreach (var child in component.VisualChildren)
        {
            TraverseVisualTreeAndUnload(child);
        }

        if (component is ObservableUIComponent observableUiComponent)
        {
            observableUiComponent.RaiseEvent(new RoutedEventArgs(UnloadedEvent, component));
        }
    }


    public AdamantiumComponent TemplatedParent { get; internal set;}

    public virtual void OnApplyTemplate()
    {
    }
}