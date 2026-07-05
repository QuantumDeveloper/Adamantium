using System;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// The live connection behind <c>{ObservableResource Key}</c>: resolves a keyed resource TREE-SCOPED from the target
/// (Local dictionaries on the target or an ancestor, then Theme, then Global), pushes it onto the target property, and
/// RE-RESOLVES whenever the resource set changes (a theme swap, or a dictionary loaded/unloaded) or the target re-parents
/// into a different Local scope. Unlike <see cref="Resources.ResourceReference"/> - a one-shot lookup resolved once on
/// attach - this stays connected, so a runtime theme switch flows straight through with no reload. Mirrors
/// <see cref="ThemeResourceExpression"/>, but its source is the ResourceManager's keyed dictionaries (tree-scoped) rather
/// than a single theme property.
/// </summary>
public class ObservableResourceExpression : BindingExpressionBase
{
    private readonly string _key;
    private readonly ValuePriority _priority;
    private readonly object _token;
    private IResourceManager _resources;

    public ObservableResourceExpression(IFundamentalUIComponent target, AdamantiumProperty targetProperty, string key,
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
        _resources = UIAppContext.Current?.ResourceManager;
        if (_resources == null || TargetProperty == null || string.IsNullOrEmpty(_key)) return;

        UpdateTarget();
        _resources.ResourcesChanged += OnResourcesChanged;

        // Re-resolve once the target is ROOTED, where its full ancestor chain - incl. a Local dictionary's owner - is
        // present: a visual element via the visual tree; a non-visual one via the LOGICAL tree (it never enters the
        // visual tree, so its visual-attach event never fires).
        if (Target is IUIComponent visual) visual.AttachedToVisualTreeEvent += OnVisualAttached;
        else if (Target != null) Target.AttachedToLogicalTree += OnLogicalAttached;

        if (Target is IInputComponent input) input.Unloaded += OnTargetUnloaded;
    }

    public override void UpdateTarget()
    {
        if (_resources == null || TargetProperty == null) return;
        var value = _resources.FindResource(Target, _key);   // tree-scoped; falls back Theme -> Global
        // A transient miss (e.g. mid theme-swap, before the new theme's dictionary is loaded) must NOT clobber the
        // property with null - keep the last good value until the resolve succeeds again.
        if (value == null) return;
        if (_priority == ValuePriority.Trigger && _token != null)
            Target.SetTriggerValue(TargetProperty, value, _token);
        else
            Target.SetValue(TargetProperty, value, _priority);
    }

    public override void CloseConnection()
    {
        if (_resources != null)
        {
            _resources.ResourcesChanged -= OnResourcesChanged;
            _resources = null;
        }
        if (Target is IUIComponent visual) visual.AttachedToVisualTreeEvent -= OnVisualAttached;
        else if (Target != null) Target.AttachedToLogicalTree -= OnLogicalAttached;
        if (Target is IInputComponent input) input.Unloaded -= OnTargetUnloaded;
    }

    private void OnResourcesChanged(object sender, EventArgs e) => UpdateTarget();
    private void OnVisualAttached(object sender, VisualTreeAttachmentEventArgs e) => UpdateTarget();
    private void OnLogicalAttached(object sender, LogicalTreeAttachmentEventArgs e) => UpdateTarget();
    private void OnTargetUnloaded(object sender, RoutedEventArgs e) => CloseConnection();
}
