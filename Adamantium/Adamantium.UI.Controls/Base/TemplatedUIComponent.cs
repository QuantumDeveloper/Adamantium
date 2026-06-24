using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls.Base;

public class TemplatedUIComponent : InputUIComponent, ITemplatedUIComponent
{
    private TemplateResult templateResult;
   
    public static readonly AdamantiumProperty TemplateProperty =
        AdamantiumProperty.Register(nameof(Template), typeof(ControlTemplate), typeof(MeasurableUIComponent),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, TemplateChangedCallback));
   
    private static void TemplateChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is TemplatedUIComponent component)
        {
            if (e.OldValue is ControlTemplate oldTemplate)
            {
                component.RemoveTemplate();
            }

            if (e.NewValue is ControlTemplate newTemplate)
            {
                component.ApplyTemplate();
            }

            // Template parts just changed: re-point any style/element triggers that target named parts at the new tree
            // (and tear down what they held on the old, now-discarded parts) so a runtime template swap stays leak-free.
            component.ReevaluateTriggersForTemplateChange();
        }
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