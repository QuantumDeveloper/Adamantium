using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

public class TemplateBindingExpression : BindingExpressionBase
{
    public AdamantiumComponent Source { get; set; }
   
    public AdamantiumProperty SourceProperty { get; set; }
   
    public TemplateBinding TemplateBinding { get; }
    
    public BindingMode Mode { get; set; }

    public TemplateBindingExpression(TemplateBinding templateBinding)
    {
        TemplateBinding = templateBinding;
        Mode = templateBinding.Mode;
    }

    public TemplateBindingExpression(AdamantiumComponent source, AdamantiumComponent target, string sourceProperty, TemplateBinding templateBinding) : this(templateBinding)
    {
        SourceProperty = source.GetProperty(sourceProperty);
        Target = target;
        TargetProperty = target.GetProperty(TemplateBinding.Path);
    }

    private void OnSourcePropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        UpdateTarget();
    }

    private void OnTargetPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        UpdateSource();
    }

    public override void UpdateSource()
    {
        Source.SetEffectiveValue(SourceProperty, Target.GetValue(TargetProperty));
    }

    public override void UpdateTarget()
    {
        Target.SetEffectiveValue(TargetProperty, Source.GetValue(SourceProperty));
    }
    
    public void Init()
    {
        SourceProperty.NotifyChanged += OnSourcePropertyChanged;
        if (Mode == BindingMode.TwoWay)
        {
            TargetProperty.NotifyChanged += OnTargetPropertyChanged;
        }
    }

    private void DeInit()
    {
        SourceProperty.NotifyChanged -= OnSourcePropertyChanged;
        if (Mode == BindingMode.TwoWay)
        {
            TargetProperty.NotifyChanged -= OnTargetPropertyChanged;
        }
    }
   
    public override void Close()
    {
        DeInit();
    }
}