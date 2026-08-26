using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

public interface IFundamentalUIComponent : IAdamantiumComponent, IDispatcherComponent, IName
{
    /// <summary>What this view asked of whoever navigates away from it (<c>x:KeepAlive</c>). Metadata: the view states
    /// what it wants, the navigator decides.</summary>
    public NavigationCacheMode KeepAlive { get; }

    public String Id { get; set; }
    /// <summary>Reading this BUILDS the collection if the component never had one - it has to, because markup adds to it
    /// through the getter. Ask <see cref="HasClassNames"/> first when all you want to know is whether it is empty.</summary>
    public Classes ClassNames { get; }

    /// <summary>Whether any class name is set, without building the collection to find out.</summary>
    public bool HasClassNames { get; }

    public StylesCollection Styles { get; }
    public void AttachStyles(params ReadOnlySpan<Style> styles);
    public void DetachStyles();
    public void DetachStyles(params ReadOnlySpan<Style> styles);
    public object DataContext { get; set; }
    public IFundamentalUIComponent LogicalParent { get; }
    public IAdamantiumComponent TemplatedParent { get; }

    public event AdamantiumPropertyChangedEventHandler DataContextChanged;

    public IReadOnlyCollection<IFundamentalUIComponent> LogicalChildren { get; }

    void AddLogicalChild(IFundamentalUIComponent child);

    void RemoveLogicalChild(IFundamentalUIComponent child);

    // Returns the base type so a MultiBinding (MultiBindingExpression) is handled too, not just single BindingExpression.
    public BindingExpressionBase SetBinding(AdamantiumProperty property, BindingBase bindingBase);

    public BindingExpressionBase SetBinding(string property, BindingBase bindingBase);
    
    public void RemoveBinding(AdamantiumProperty property);
    
    public void RemoveBinding(string property);

    public void InvalidateStyles();
    
    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;
}
