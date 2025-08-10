using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.Extensions;
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
      
        templateResult = Template.Build();
        if (templateResult != null)
        {
            AddTemplateChild(templateResult.RootComponent);
            // TODO: применить здесь триггеры
            OnApplyTemplate();
        }
    }

    private void RemoveTemplate()
    {
        templateResult = null;
        RemoveVisualChildren();
        OnRemoveTemplate();
    }
    
    protected void AddTemplateChild(IUIComponent child)
    {
        child.TraverseVisualTree(component =>
        {
            var fundamental = (FundamentalUIComponent)component;
            fundamental.TemplatedParent = this;
        });
        AddVisualChild(child);
    }

    public virtual void OnRemoveTemplate()
    {
        RaiseEvent(new RoutedEventArgs(UnloadedEvent, this));
    }

    public AdamantiumComponent TemplatedParent { get; internal set;}

    public virtual void OnApplyTemplate()
    {
    }
}