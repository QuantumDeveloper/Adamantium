using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

public class ContentPresenter : InputUIComponent
{
    // The currently hosted content (the incoming one while a transition runs) and, when it came from a DataTemplate,
    // the result to destroy on replacement. While a transition plays we also keep the previous content alive as the
    // "outgoing" pair so it can slide out before being removed.
    private IUIComponent _currentRoot;
    private TemplateResult _currentTemplateResult;
    private IUIComponent _outgoingRoot;
    private TemplateResult _outgoingTemplateResult;
    private bool _isContentChanged;
    private bool _transitionPending;

    public static readonly AdamantiumProperty ContentProperty = AdamantiumProperty.Register(nameof(Content),
        typeof(object), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnContentPropertyChanged));

    public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
        typeof(DataTemplate), typeof(ContentPresenter), new PropertyMetadata(null, OnContentTemplateChanged));

    public static readonly AdamantiumProperty ContentTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ContentTemplateSelector),
        typeof(DataTemplateSelector), typeof(ContentPresenter), new PropertyMetadata(null, OnContentTemplateSelectorChanged));

    public static readonly AdamantiumProperty ContentTransitionProperty = AdamantiumProperty.Register(nameof(ContentTransition),
        typeof(ContentTransition), typeof(ContentPresenter), new PropertyMetadata(ContentTransition.None));

    public static readonly AdamantiumProperty TransitionDurationProperty = AdamantiumProperty.Register(nameof(TransitionDuration),
        typeof(Double), typeof(ContentPresenter), new PropertyMetadata(0.25));

    private static void OnContentPropertyChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ContentPresenter presenter)
        {
            presenter.OnContentChangedInternal(e.OldValue, e.NewValue);
        }
    }

    private void OnContentChangedInternal(object oldContent, object newContent)
    {
        _isContentChanged = true;
    }

    private void UpdateVisualContent(object newContent)
    {
        if (!_isContentChanged)
            return;

        _isContentChanged = false;

        // A new swap supersedes one still mid-flight: finish the previous transition instantly before starting over.
        if (_outgoingRoot != null)
            RemoveOutgoing();

        // Animate when a transition is selected and there is new content - including the FIRST content. In the designer
        // it plays only for the LIVE previewer; a one-shot render shows the settled state (not a mid-slide capture).
        var animate = ContentTransition != ContentTransition.None && newContent != null
                      && (!Design.IsDesignMode || Design.IsLivePreview);

        if (animate)
        {
            // Keep the current content (if any) as outgoing; it slides out and is removed when its animation completes.
            _outgoingRoot = _currentRoot;
            _outgoingTemplateResult = _currentTemplateResult;
        }
        else if (_currentRoot != null)
        {
            RemoveVisualChild(_currentRoot);
            RemoveLogicalChild(_currentRoot);
            _currentTemplateResult?.Destroy();
        }

        _currentRoot = null;
        _currentTemplateResult = null;

        if (newContent != null)
            BuildCurrent(newContent);

        // The slide distance is the laid-out size, so defer starting it to the next arrange (size known there). If a
        // transition was expected but produced no new root, drop the kept-alive outgoing content now.
        _transitionPending = animate && _currentRoot != null;
        if (animate && !_transitionPending)
            RemoveOutgoing();
    }

    private void BuildCurrent(object newContent)
    {
        if (newContent is IUIComponent iuiComponent)
        {
            // Host the content element itself (the SAME instance the author built and bound), not a template result,
            // so a {Binding} set on the authored control lives on the element that is actually rendered/clicked.
            _currentRoot = iuiComponent;
        }
        else
        {
            var dataTemplate = ContentTemplate ?? ContentTemplateSelector?.SelectTemplate(newContent, this);
            if (dataTemplate != null)
            {
                _currentTemplateResult = dataTemplate.Build(this);
                _currentRoot = _currentTemplateResult?.RootComponent;
            }
            else
            {
                _currentRoot = new TextBlock { Text = newContent.ToString(), FontSize = 28 };
            }
        }

        if (_currentRoot != null)
        {
            AddVisualChild(_currentRoot);
            AddLogicalChild(_currentRoot);
        }
    }

    private void RemoveOutgoing()
    {
        if (_outgoingRoot == null)
            return;

        RemoveVisualChild(_outgoingRoot);
        RemoveLogicalChild(_outgoingRoot);
        _outgoingTemplateResult?.Destroy();
        _outgoingRoot = null;
        _outgoingTemplateResult = null;
    }

    // Starts the slide once the children have been arranged at the full presenter rect (same origin), so the only
    // thing moving them is the animated RenderTransform - layout is untouched. Shared logic lives in ContentTransitions.
    private void StartTransition(Size size)
    {
        // Sliding content can extend past the presenter while it moves; clip it (enforcement depends on renderer clip).
        ClipToBounds = true;
        ContentTransitions.Run(ContentTransition, TransitionDuration, size, _currentRoot, _outgoingRoot, RemoveOutgoing);
    }

    private static void OnContentTemplateChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ContentPresenter presenter)
        {
            presenter.OnContentChangedInternal(e.OldValue, e.NewValue);
        }
    }

    private static void OnContentTemplateSelectorChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ContentPresenter presenter)
        {
            presenter.OnContentChangedInternal(e.OldValue, e.NewValue);
        }
    }

    [Content]
    public object Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public DataTemplate ContentTemplate
    {
        get => GetValue<DataTemplate>(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public DataTemplateSelector ContentTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ContentTemplateSelectorProperty);
        set => SetValue(ContentTemplateSelectorProperty, value);
    }

    public ContentTransition ContentTransition
    {
        get => GetValue<ContentTransition>(ContentTransitionProperty);
        set => SetValue(ContentTransitionProperty, value);
    }

    public Double TransitionDuration
    {
        get => GetValue<Double>(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateVisualContent(Content);
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        if (_transitionPending)
        {
            _transitionPending = false;
            StartTransition(finalSize);
        }

        return size;
    }
}
