using System;
using System.Collections.Generic;
using Adamantium.UI.Controls;
using Adamantium.UI.Data;
using Adamantium.UI.Input;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public interface IFrameworkComponent : IUIComponent, IInputElement
{
    object DataContext { get; set; }
    Double Width { get; set; }
    Double Height { get; set; }
    Double ActualWidth { get; }
    Double ActualHeight { get; }
    Double MinWidth { get; set; }
    Double MinHeight { get; set; }
    Double MaxWidth { get; set; }
    Double MaxHeight { get; set; }
    Thickness Margin { get; set; }
    VerticalAlignment VerticalAlignment { get; set; }
    HorizontalAlignment HorizontalAlignment { get; set; }
    object Tag { get; set; }
    FrameworkComponent Parent { get; }
    ControlTemplate Template { get; set; }
    IReadOnlyCollection<FrameworkComponent> LogicalChildrenCollection { get; }
    
    BindingExpression SetBinding(AdamantiumProperty property, BindingBase bindingBase);
    
    event AdamantiumPropertyChangedEventHandler DataContextChanged;

    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;

    void OnApplyTemplate();
    
    void OnRemoveTemplate();

    IAdamantiumComponent GetTemplateChild(string name);
}