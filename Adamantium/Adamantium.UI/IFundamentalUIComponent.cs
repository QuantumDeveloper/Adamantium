using System;
using System.Collections.Generic;
using Adamantium.UI.Data;
using Adamantium.UI.Resources;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public interface IFundamentalUIComponent : IAdamantiumComponent, IName
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
    
    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;
}
