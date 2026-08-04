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

    public TemplateBindingExpression(IFundamentalUIComponent source, IAdamantiumComponent target, string targetProperty, TemplateBinding templateBinding) : this(templateBinding)
    {
        // {TemplateBinding Path} on a part's property: read Path from the templated parent (source) and write the part's
        // declared property (targetProperty). The generator passes propRef.Name (the part attribute) as targetProperty
        // and the markup arg as Path - so source resolves from Path, target from targetProperty. These differ for a
        // cross-name binding (e.g. ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}").
        SourceProperty = source == null ? null : AdamantiumPropertyMap.ResolveProperty(source.GetType(), TemplateBinding.Path);
        SourcePropertyName = TemplateBinding.Path;
        Target = target;
        TargetProperty = target.GetProperty(targetProperty);
    }

    private void OnSourcePropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != SourceProperty) return;   // this source instance raises PropertyChanged for ALL its properties
        UpdateTarget();
    }

    private void OnTargetPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != TargetProperty) return;
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
        if (SourceProperty == null && Source != null)
        {
            SourceProperty = AdamantiumPropertyMap.ResolveProperty(Source.GetType(), SourcePropertyName);
        }
        UpdateTarget();
        // Subscribe to the SOURCE INSTANCE's change event, not the property's global AdamantiumProperty.Changed. The
        // latter fires for EVERY control that shares this AdamantiumProperty, so one templated control's Content change
        // would run UpdateTarget() on every {TemplateBinding Content} in the whole app (O(all such bindings) per set - the
        // scroll rebind hot path did this ~200x/frame). The instance event scopes the notification to this binding's own source.
        Source.PropertyChanged += OnSourcePropertyChanged;
        if (Mode == BindingMode.TwoWay)
        {
            Target.PropertyChanged += OnTargetPropertyChanged;
        }
    }

    private void Destroy()
    {
        if (Source != null)
        {
            Source.PropertyChanged -= OnSourcePropertyChanged;
        }

        if (Mode == BindingMode.TwoWay && Target != null)
        {
            Target.PropertyChanged -= OnTargetPropertyChanged;
        }
    }
   
    public override void CloseConnection()
    {
        Destroy();
    }
}