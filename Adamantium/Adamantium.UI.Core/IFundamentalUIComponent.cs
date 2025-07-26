using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

public interface IFundamentalUIComponent : IAdamantiumComponent, IDispatcherComponent, IName
{
    public String Id { get; set; }
    public Classes ClassNames { get; }
    public StylesCollection Styles { get; }
    public void AttachStyles(params Style[] styles);
    public void DetachStyles();
    public void DetachStyles(params Style[] styles);
    public object DataContext { get; set; }
    public IFundamentalUIComponent LogicalParent { get; }

    public event AdamantiumPropertyChangedEventHandler DataContextChanged;

    public IReadOnlyCollection<IFundamentalUIComponent> LogicalChildren { get; }

    public BindingExpression SetBinding(AdamantiumProperty property, BindingBase bindingBase);
    
    public BindingExpression SetBinding(string property, BindingBase bindingBase);
    
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
