using System;
using System.Collections.Generic;
using Adamantium.UI.Data;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public interface IFundamentalUIComponent : IAdamantiumComponent, IName
{
    public String Uid { get; set; }
    public StylesCollection Styles { get; }
    public object DataContext { get; set; }
    public IFundamentalUIComponent LogicalParent { get; }

    public event AdamantiumPropertyChangedEventHandler DataContextChanged;

    public IReadOnlyCollection<IFundamentalUIComponent> LogicalChildren { get; }

    public BindingExpression SetBinding(AdamantiumProperty property, BindingBase bindingBase);
    
    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;
}
