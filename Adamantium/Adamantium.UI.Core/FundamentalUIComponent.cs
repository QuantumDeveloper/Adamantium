using System.Collections.Specialized;
using Adamantium.Core.Collections;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

public abstract class FundamentalUIComponent : AnimatableUIComponent, IFundamentalUIComponent
{
    private readonly StylesCollection _attachedStyles;
    private IFundamentalUIComponent parent;
    private TrackingCollection<IFundamentalUIComponent> logicalChildren;
    
    public static readonly AdamantiumProperty NameProperty = AdamantiumProperty.Register(nameof(Name),
        typeof(String), typeof(FundamentalUIComponent), new PropertyMetadata(String.Empty));
        
    public static readonly AdamantiumProperty StylesProperty =
            AdamantiumProperty.RegisterReadOnly(nameof(Styles), typeof(StylesCollection), typeof(FundamentalUIComponent));
    
    public static readonly AdamantiumProperty BehaviorsProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(Behaviors), typeof(BehaviorCollection), typeof(FundamentalUIComponent));
    
    public static readonly AdamantiumProperty DataContextProperty = AdamantiumProperty.Register(nameof(DataContext),
        typeof(object), typeof(FundamentalUIComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits, DataContextChangedCallBack));
    
    public static readonly AdamantiumProperty ClassesProperty = AdamantiumProperty.Register(nameof(Classes),
        typeof(Classes), typeof(FundamentalUIComponent),
        new PropertyMetadata(new Classes(), ClassesChangedCallBack));
    
    public static readonly AdamantiumProperty UidProperty = AdamantiumProperty.Register(nameof(Id),
        typeof(String), typeof(FundamentalUIComponent), new PropertyMetadata(String.Empty));
    
    public static readonly AdamantiumProperty AllowDropProperty = AdamantiumProperty.Register(nameof(AllowDrop),
        typeof(Boolean), typeof(FundamentalUIComponent), new PropertyMetadata(true));

    private static void DataContextChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumAdamantiumComponent as FundamentalUIComponent;
        o?.DataContextChanged?.Invoke(o, e);
        o?.UpdateDataContext();
    }
    
    private static void ClassesChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumAdamantiumComponent as FundamentalUIComponent;
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
        Behaviors = new BehaviorCollection(this);
    }

    public BehaviorCollection Behaviors
    {
        get => GetValue<BehaviorCollection>(BehaviorsProperty);
        private init => SetValue(BehaviorsProperty, value);
    }

    private void UpdateDataContext()
    {
        // Re-resolve this element's own bindings against the new context. Propagation to logical children is handled by
        // value inheritance (InheritanceParent -> ParentPropertyChanged), not an explicit push — which also keeps a
        // child's locally-set DataContext intact and works regardless of build order.
        BindingEngine.RefreshBindings(this);
    }

    private void StylesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                var styles = (IEnumerable<Style>)e.NewItems;
                Style.Apply(this, styles?.ToArray());
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                var styles = (IEnumerable<Style>)e.NewItems;
                Style.UnApply(this, styles?.ToArray());
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

    private bool _viewModelApplied;

    /// <summary>
    /// The view-model type declared in markup via <c>x:ViewModel</c>. Generated code-behind overrides this to return
    /// the authored type; the default is null. It is pure metadata - the control does NOT hold a container. When the
    /// element becomes live (attached to the tree / window initialized) the framework resolves an instance from the
    /// app's dependency resolver and assigns it as <see cref="DataContext"/> (see <see cref="ApplyViewModel"/>).
    /// </summary>
    public virtual Type ViewModelType => null;

    /// <summary>
    /// Resolves the <see cref="ViewModelType"/> (if any) from the application's dependency resolver and assigns it as
    /// the DataContext - but only the first time, and only if the DataContext wasn't set explicitly (so x:ViewModel is
    /// the default, an explicit DataContext or binding still wins, which keeps a view reusable across view-models).
    /// Reached through the same ambient <see cref="UIAppContext.Current"/> the framework already uses for theming, so
    /// the control never stores the container. No-op when no resolver is available (e.g. the headless designer).
    /// </summary>
    protected void ApplyViewModel()
    {
        if (_viewModelApplied) return;

        var viewModelType = ViewModelType;
        if (viewModelType == null) return;

        if (HasExplicitValue(DataContextProperty))
        {
            _viewModelApplied = true;   // caller already provided a DataContext; don't override it
            return;
        }

        var context = UIAppContext.Current?.UIContext;
        if (context == null) return;    // app context not ready yet; a later attach will retry

        DataContext = context.Resolve(viewModelType);
        _viewModelApplied = true;
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
    
    public bool IsStyleApplied { get; private set; }

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
    
    public IAdamantiumComponent TemplatedParent { get; internal set; }

    public BindingExpression SetBinding(string property, BindingBase bindingBase)
    {
        var adamantiumProperty = GetProperty(property);
        return SetBinding(adamantiumProperty, bindingBase);
    }
    
    public BindingExpression SetBinding(AdamantiumProperty property, BindingBase bindingBase)
    {
        // Bindings live in the central BindingEngine registry (queryable/refreshable), not as element-private state.
        return (BindingExpression)BindingEngine.SetBinding(this, property, bindingBase);
    }
    
    public void RemoveBinding(string property)
    {
        var adamantiumProperty = GetProperty(property);
    }

    public void RemoveBinding(AdamantiumProperty property)
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

    public void AddLogicalChild(IFundamentalUIComponent child)
    {
        LogicalChildrenCollection.Add(child);
    }
    
    public void RemoveLogicalChild(IFundamentalUIComponent child)
    {
        LogicalChildrenCollection.Remove(child);
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
                throw new InvalidOperationException("The Control already has a parent.Parent Element is: " +
                                                    LogicalParent);
            }

            parent = logicalParent;
            // Wire value inheritance (DataContext and any other Inherits property) to the new logical parent; null on
            // detach. This is what carries a window's DataContext down to its children so their {Binding}s resolve.
            InheritanceParent = logicalParent as AdamantiumComponent;

            //var root = FindStyleRoot(old);

            if (old != null)
            {
                var e = new LogicalTreeAttachmentEventArgs(LogicalParent);
                OnDetachedFromLogicalTree(e);
            }

            if (parent != null)
            {
                ApplyCurrentTheme();

                var e = new LogicalTreeAttachmentEventArgs(parent);
                OnAttachedToLogicalTree(e);
            }
        }
    }

    public void ApplyCurrentTheme()
    {
        if (UIAppContext.Current == null)
            return;
        
        UIAppContext.Current.UIContext.ThemeContext.ApplyCurrentTheme(this);
        UIAppContext.Current.UIContext.ThemeContext.ApplyExternalStyles(this, Styles.ToArray());
        
        IsStyleApplied = true;
        
        //((IMeasurableComponent)this).InvalidateMeasure();
    }
    
    public void InvalidateStyles()
    {
        if (UIAppContext.Current == null) 
            return;

        IsStyleApplied = false;
        
        // UIAppContext.Current.UIContext.ThemeContext.ApplyCurrentTheme(this);
        // UIAppContext.Current.UIContext.ThemeContext.ApplyExternalStyles(this, Styles.ToArray());
        
        foreach (var component in LogicalChildrenCollection)
        {
            component.InvalidateStyles();
        }
    }

    protected virtual void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        // A nested view (created via 'new' by its parent's generated code) declares its view-model with x:ViewModel;
        // resolve it now that the element is part of a live tree and the app context is reachable.
        ApplyViewModel();
    }

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

    public void VerifyAccess()
    {
        UIAppContext.Current.Dispatcher.VerifyAccess();
    }

    public bool CheckAccess() => UIAppContext.Current.Dispatcher.CheckAccess();
}
