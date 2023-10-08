using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.UI.Controls;
using Adamantium.UI.Data;
using Adamantium.UI.Resources;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public class FundamentalUIComponent : AnimatableUIComponent, IFundamentalUIComponent
{
    private StylesCollection _attachedStyles;
    private IFundamentalUIComponent parent;
    private TrackingCollection<IFundamentalUIComponent> logicalChildren;
    
    public static readonly AdamantiumProperty NameProperty = AdamantiumProperty.Register(nameof(Name),
        typeof(String), typeof(FundamentalUIComponent), new PropertyMetadata(String.Empty));
        
    public static readonly AdamantiumProperty StylesProperty =
            AdamantiumProperty.RegisterReadOnly(nameof(Styles), typeof(StylesCollection), typeof(FundamentalUIComponent));
    
    public static readonly AdamantiumProperty DataContextProperty = AdamantiumProperty.Register(nameof(DataContext),
        typeof(object), typeof(FundamentalUIComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits, DataContextChangedCallBack));

    public static readonly AdamantiumProperty ClassesProperty = AdamantiumProperty.Register(nameof(Classes),
        typeof(Classes), typeof(FundamentalUIComponent),
        new PropertyMetadata(new Classes(), ClassesChangedCallBack));
    
    public static readonly AdamantiumProperty UidProperty = AdamantiumProperty.Register(nameof(Id),
        typeof(String), typeof(FundamentalUIComponent), new PropertyMetadata(String.Empty));
    
    public static readonly AdamantiumProperty AllowDropProperty = AdamantiumProperty.Register(nameof(AllowDrop),
        typeof(Boolean), typeof(UIComponent), new PropertyMetadata(true));

    private static void DataContextChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumAdamantiumComponent as MeasurableUIComponent;
        o?.DataContextChanged?.Invoke(o, e);
    }
    
    private static void ClassesChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumAdamantiumComponent as MeasurableUIComponent;
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
        ClassNames = new Classes();
        Styles = new StylesCollection();
        Styles.CollectionChanged += StylesOnCollectionChanged;
        _attachedStyles = new StylesCollection();
    }

    private void StylesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                var styles = (IEnumerable<Style>)e.NewItems;
                Style.Apply(this, styles.ToArray());
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                var styles = (IEnumerable<Style>)e.NewItems;
                Style.UnApply(this, styles.ToArray());
                break;
            }
        }
    }

    private void ClassesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        
    }
    
    public Boolean AllowDrop
    {
        get => GetValue<Boolean>(AllowDropProperty);
        set => SetValue(AllowDropProperty, value);
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

    public string Id
    {
        get => GetValue<string>(UidProperty);
        set => SetValue(UidProperty, value);
    }

    public Classes ClassNames { get; }

    public StylesCollection Styles
    {
        get => GetValue<StylesCollection>(StylesProperty);
        private init => SetValue(StylesProperty, value);
    }

    public void AttachStyles(params Style[] styles)
    {
        foreach (var style in styles)
        {
            style.Attach(this);
        }
    }

    public void DetachStyles()
    {
        foreach (var style in _attachedStyles)
        {
            style.Detach(this);
        }
        _attachedStyles.Clear();
    }
    
    public void DetachStyles(params Style[] styles)
    {
        foreach (var style in styles)
        {
            style.Detach(this);
            _attachedStyles.Remove(style);
        }
    }

    public IFundamentalUIComponent LogicalParent => parent;
    
    public BindingExpression SetBinding(AdamantiumProperty property, BindingBase bindingBase)
    {
        return null;
    }

    public BindingExpression SetBinding(string property, BindingBase bindingBase)
    {
        throw new NotImplementedException();
    }

    public void RemoveBinding(AdamantiumProperty property)
    {
        throw new NotImplementedException();
    }

    public void RemoveBinding(string property)
    {
        throw new NotImplementedException();
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

            // TODO: define do we actually need InheritanceParent property
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
