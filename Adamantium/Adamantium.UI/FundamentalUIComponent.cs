using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.UI.Controls;
using Adamantium.UI.Data;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public class FundamentalUIComponent : AdamantiumComponent, IFundamentalUIComponent
{
    private IFundamentalUIComponent parent;
    private TrackingCollection<IFundamentalUIComponent> logicalChildren;
    
    public static readonly AdamantiumProperty NameProperty = AdamantiumProperty.Register(nameof(Name),
        typeof(String), typeof(FundamentalUIComponent), new PropertyMetadata(String.Empty));
        
    public static readonly AdamantiumProperty StyleProperty =
            AdamantiumProperty.Register(nameof(Style), typeof(Style), typeof(FundamentalUIComponent),
                new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, StyleChangedCallback));
    
    public static readonly AdamantiumProperty DataContextProperty = AdamantiumProperty.Register(nameof(DataContext),
        typeof(object), typeof(MeasurableComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits, DataContextChangedCallBack));

    public static readonly AdamantiumProperty ClassesProperty = AdamantiumProperty.Register(nameof(Classes), typeof(Classes), typeof(FundamentalUIComponent),
        new PropertyMetadata(new Classes(), ClassesChangedCallBack));
    
    private static void DataContextChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumObject as MeasurableComponent;
        o?.DataContextChanged?.Invoke(o, e);
    }
    
    private static void StyleChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is IUIComponent component && e.NewValue != null)
        {
            component.Style.Attach(component);
        }
    }

    private static void ClassesChangedCallBack(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumObject as MeasurableComponent;
        if (o == null) return;

        if (e.OldValue != null)
        {
            var classes = (Classes)e.NewValue;
            classes.CollectionChanged -= o.ClassesCollectionChanged;
        }

        if (e.NewValue != null)
        {
            var classes = (Classes)e.NewValue;
            classes.CollectionChanged += o.ClassesCollectionChanged;
        }
    }

    public FundamentalUIComponent()
    {
    }

    private void ClassesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        
    }

    public Classes Classes
    {
        get => GetValue<Classes>(ClassesProperty);
        set => SetValue(ClassesProperty, value);
    }

    public object DataContext
    {
        get => GetValue(DataContextProperty); 
        set => SetValue(DataContextProperty, value);
    }

    public Style Style
    {
        get => GetValue<Style>(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    public IFundamentalUIComponent LogicalParent => parent;
    
    public BindingExpression SetBinding(AdamantiumProperty property, BindingBase bindingBase)
    {
        return null;
    }

    public event AdamantiumPropertyChangedEventHandler DataContextChanged;
   
    public IReadOnlyCollection<IFundamentalUIComponent> LogicalChildren => LogicalChildrenCollection.AsReadOnly();
    
    protected TrackingCollection<IFundamentalUIComponent> LogicalChildrenCollection
    {
        get
        {
            if (logicalChildren == null)
            {
                var list = new TrackingCollection<IFundamentalUIComponent>();
                LogicalChildrenCollection = list;
            }
            return logicalChildren;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (logicalChildren != value && logicalChildren != null)
            {
                logicalChildren.CollectionChanged -= LogicalChildrenCollectionChanged;
            }

            logicalChildren = value;
            logicalChildren.CollectionChanged += LogicalChildrenCollectionChanged;
        }

    }
    
    public String Name
    {
        get => GetValue<String>(NameProperty);
        set => SetValue(NameProperty, value);
    }
    
    private void LogicalChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                SetLogicalParent(e.NewItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Remove:
                ClearLogicalParent(e.OldItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Replace:
                ClearLogicalParent(e.OldItems.Cast<FundamentalUIComponent>());
                SetLogicalParent(e.NewItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Reset:
                throw new NotSupportedException("Reset should not be signalled on LogicalChildren collection");
        }
    }

    private void SetLogicalParent(IEnumerable<FundamentalUIComponent> children)
    {
        foreach (var element in children)
        {
            element.SetParent(this);
        }
    }

    private void ClearLogicalParent(IEnumerable<FundamentalUIComponent> children)
    {
        foreach (var element in children)
        {
            if (element.LogicalParent == this)
            {
                element.SetParent(null);
            }
        }
    }
    
    /// <summary>
    /// Sets the control's logical parent.
    /// </summary>
    /// <param name="logicalParent">The parent.</param>
    private void SetParent(IFundamentalUIComponent logicalParent)
    {
        var old = LogicalParent;

        if (logicalParent != old)
        {
            if (old != null && logicalParent != null)
            {
                throw new InvalidOperationException("The Control already has a parent.Parent Element is: " + LogicalParent);
            }

            // TODO: define do we actually need InheritanceParent proprety
            //InheritanceParent = parent;
            parent = logicalParent;

            /*
            var root = FindStyleRoot(old);

            if (root != null)
            {
               var e = new LogicalTreeAttachmentEventArgs(root);
               OnDetachedFromLogicalTree(e);
            }

            root = FindStyleRoot(this);

            if (root != null)
            {
               var e = new LogicalTreeAttachmentEventArgs(root);
               OnAttachedToLogicalTree(e);
            }

            RaisePropertyChanged(ParentProperty, old, _parent, BindingPriority.LocalValue);
            */
        }
    }


    protected virtual void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    { }

    protected virtual void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    { }

    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;

}
