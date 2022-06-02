using System;
using System.Collections.Generic;
using Adamantium.UI.Data;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public interface IFundamentalUIComponent : IAdamantiumComponent, IName
{
    Style Style { get; set; }
    object DataContext { get; set; }
    IFundamentalUIComponent LogicalParent { get; }
    
    event AdamantiumPropertyChangedEventHandler DataContextChanged;
    
    IReadOnlyCollection<IFundamentalUIComponent> LogicalChildren { get; }
    
    BindingExpression SetBinding(AdamantiumProperty property, BindingBase bindingBase);
    
    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;
}
