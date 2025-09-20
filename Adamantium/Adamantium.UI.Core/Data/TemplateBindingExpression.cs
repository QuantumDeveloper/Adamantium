using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

public class TemplateBindingExpression : BindingExpressionBase
{
    public IFundamentalUIComponent Source { get; set; }
   
    public AdamantiumProperty SourceProperty { get; set; }

    public string SourcePropertyName { get; set; }
   
    public TemplateBinding TemplateBinding { get; }
    
    public BindingMode Mode { get; set; }

    public TemplateBindingExpression(TemplateBinding templateBinding)
    {
        TemplateBinding = templateBinding;
        Mode = templateBinding.Mode;
    }

    public TemplateBindingExpression(IFundamentalUIComponent source, IFundamentalUIComponent target, string sourceProperty, TemplateBinding templateBinding) : this(templateBinding)
    {
        SourceProperty = source?.GetProperty(sourceProperty);
        SourcePropertyName = sourceProperty;
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

    public override void EstablishConnection()
    {
        Init();
    }

    public override void UpdateSource()
    {
        Source.SetValue(SourceProperty, Target.GetValue(TargetProperty), ValuePriority.Template);
    }

    public override void UpdateTarget()
    {
        Target.SetValue(TargetProperty, Source.GetValue(SourceProperty), ValuePriority.Template);
    }
    
    private void Init()
    {
        Destroy();
        if (SourceProperty == null)
        {
            SourceProperty = Source?.GetProperty(SourcePropertyName);
        }
        UpdateTarget();
        SourceProperty.NotifyChanged += OnSourcePropertyChanged;
        if (Mode == BindingMode.TwoWay)
        {
            TargetProperty.NotifyChanged += OnTargetPropertyChanged;
        }
    }

    private void Destroy()
    {
        if (SourceProperty != null)
        {
            SourceProperty.NotifyChanged -= OnSourcePropertyChanged;
        }

        if (Mode == BindingMode.TwoWay && TargetProperty != null)
        {
            TargetProperty.NotifyChanged -= OnTargetPropertyChanged;
        }
    }
   
    public override void CloseConnection()
    {
        Destroy();
    }
}