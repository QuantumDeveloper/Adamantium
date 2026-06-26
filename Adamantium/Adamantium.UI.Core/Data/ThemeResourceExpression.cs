using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// The live connection behind <c>{ThemeResource Key}</c>: pushes a brush property of the ACTIVE theme onto a target
/// property and refreshes it whenever that theme property changes - so accent/focus edits apply at runtime with no
/// theme reload. Mirrors <see cref="TemplateBindingExpression"/>, but its source is the global current theme (not a
/// templated parent), and it detaches itself when the target unloads.
/// </summary>
public class ThemeResourceExpression : BindingExpressionBase
{
    private readonly string _key;
    private readonly ValuePriority _priority;
    private readonly object _token;
    private AdamantiumComponent _theme;
    private AdamantiumProperty _sourceProperty;

    public ThemeResourceExpression(IFundamentalUIComponent target, AdamantiumProperty targetProperty, string key,
        ValuePriority priority = ValuePriority.Template, object token = null)
    {
        Target = target;
        TargetProperty = targetProperty;
        _key = key;
        _priority = priority;
        _token = token;
    }

    public override void EstablishConnection()
    {
        CloseConnection();
        _theme = UIAppContext.Current?.ThemeManager?.CurrentTheme as AdamantiumComponent;
        if (_theme == null || TargetProperty == null || string.IsNullOrEmpty(_key)) return;

        _sourceProperty = AdamantiumPropertyMap.FindRegistered(_theme.GetType(), _key);
        if (_sourceProperty == null) return;

        UpdateTarget();
        _theme.PropertyChanged += OnThemePropertyChanged;
        if (Target is IInputComponent input) input.Unloaded += OnTargetUnloaded;
    }

    public override void UpdateTarget()
    {
        if (_theme == null || _sourceProperty == null || TargetProperty == null) return;
        var value = _theme.GetValue(_sourceProperty);
        if (value == null) return;
        // A trigger {ThemeResource} pushes onto the per-token trigger stack (so it coexists with other triggers on the
        // same property); a base/template one writes its priority slot directly.
        if (_priority == ValuePriority.Trigger && _token != null)
            Target.SetTriggerValue(TargetProperty, value, _token);
        else
            Target.SetValue(TargetProperty, value, _priority);
    }

    public override void CloseConnection()
    {
        if (_theme != null)
        {
            _theme.PropertyChanged -= OnThemePropertyChanged;
            _theme = null;
        }
        if (Target is IInputComponent input) input.Unloaded -= OnTargetUnloaded;
    }

    private void OnThemePropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == _sourceProperty) UpdateTarget();
    }

    private void OnTargetUnloaded(object sender, RoutedEventArgs e) => CloseConnection();
}
