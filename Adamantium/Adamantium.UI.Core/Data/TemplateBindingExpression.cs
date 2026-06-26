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

    public TemplateBindingExpression(IFundamentalUIComponent source, IFundamentalUIComponent target, string targetProperty, TemplateBinding templateBinding) : this(templateBinding)
    {
        // {TemplateBinding Path} on a part's property: read Path from the templated parent (source) and write the part's
        // declared property (targetProperty). The generator passes propRef.Name (the part attribute) as targetProperty
        // and the markup arg as Path - so source resolves from Path, target from targetProperty. These differ for a
        // cross-name binding (e.g. ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}").
        SourceProperty = source?.GetProperty(TemplateBinding.Path);
        SourcePropertyName = TemplateBinding.Path;
        Target = target;
        TargetProperty = target.GetProperty(targetProperty);
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