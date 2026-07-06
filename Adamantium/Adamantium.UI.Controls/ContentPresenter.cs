using Adamantium.Graphics.Fonts;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

public class ContentPresenter : InputUIComponent
{
    // A content presenter is a passive host for another element - never a keyboard-focus target. Comes for free from the
    // Focusable=false default (see InputUIComponent); covers ScrollContentPresenter and any templated content host.

    // The currently hosted content (the incoming one while a transition runs) and, when it came from a DataTemplate,
    // the result to destroy on replacement. While a transition plays we also keep the previous content alive as the
    // "outgoing" pair so it can slide out before being removed.
    private IUIComponent _currentRoot;
    private TemplateResult _currentTemplateResult;
    private DataTemplate _currentTemplate;   // the template that built _currentRoot, so a recycled container can reuse it
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

    // Text styling for string content (the auto-generated TextBlock). Template-bound from the templated parent (e.g. a
    // Button's Foreground/FontSize) so theme states - Pressed/Disabled text colour - drive it.
    public static readonly AdamantiumProperty ForegroundProperty = AdamantiumProperty.Register(nameof(Foreground),
        typeof(Brush), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnTextStyleChanged));

    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(Double), typeof(ContentPresenter), new PropertyMetadata(14.0, PropertyMetadataOptions.AffectsMeasure, OnTextStyleChanged));

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

        // WPF ContentPresenter semantics: when the content is a DATA object (not a UI element) rendered through a
        // ContentTemplate/ContentTemplateSelector, the presenter's DataContext IS that object, so the template's
        // {Binding}s resolve against it. This is what lets a data-templated body/header bind to its own item view-model
        // even when the presenter sits in an outer template (e.g. a TabControl's PART_SelectedContentHost, whose ambient
        // DataContext is the TabControl's, not the selected tab's). A UI-element (or null) content is left to inherit the
        // ambient DataContext - the element brought its own bindings. Only SET (never clear), so an externally assigned
        // DataContext (ItemsControl.PrepareContainer sets it before Content) is matched, never clobbered.
        //
        // NOTE: this handler is SHARED by the Content, ContentTemplate and ContentTemplateSelector changes, so it must key
        // off the actual Content - never the incoming value, which for a template/selector change is the template/selector
        // itself (that bug set DataContext to the DataTemplate, so a header/body template's {Binding}s resolved to nothing).
        if (Content is not null and not IUIComponent)
        {
            DataContext = Content;
        }
    }

    private void UpdateVisualContent(object newContent)
    {
        if (!_isContentChanged)
            return;

        _isContentChanged = false;

        // Recycling fast-path: the new content uses the SAME DataTemplate that built the current visual (a virtualized
        // list rebinding a recycled container to another item). Keep the visual AND its render units/GPU buffers - the
        // data updates by itself via the container's DataContext (the item template's {Binding}s re-resolve). Rebuilding
        // here would dispose and recreate every buffer each scroll frame (the OutOfDeviceMemory under fast scroll).
        if (newContent != null && newContent is not IUIComponent && _currentRoot != null && _currentTemplate != null)
        {
            var reuseTemplate = ContentTemplate ?? ContentTemplateSelector?.SelectTemplate(newContent, this);
            if (ReferenceEquals(reuseTemplate, _currentTemplate))
                return;   // reuse: no teardown, no rebuild
        }

        // Recycling fast-path 2: plain (no-template) content already hosted in the auto-generated TextBlock - just update
        // its text instead of tearing it down and building a new one. Without this, a virtualized list of STRING items
        // (no ItemTemplate) recreates a TextBlock - and its render units / GPU buffers - on every recycle/rebind during
        // scroll, which exhausts device memory (the OutOfDeviceMemory the template fast-path above already guards against,
        // but only for DataTemplate content).
        if (newContent != null && newContent is not IUIComponent && _currentTemplate == null
            && _currentRoot is TextBlock reusableText
            && ContentTemplate == null && ContentTemplateSelector == null)
        {
            reusableText.Text = newContent.ToString();
            return;
        }

        // A new swap supersedes one still mid-flight: finish the previous transition instantly before starting over.
        if (_outgoingRoot != null)
            RemoveOutgoing();

        // Animate when a transition is selected and there is new content - including the FIRST content. NEVER in the
        // designer (live or one-shot): a content transition is triggered by content replacement, but in the previewer
        // that only happens on initial load / a live-reconcile re-apply, not a real user-driven swap - so playing it
        // just slides the content off-screen and the captured frame(s) come back blank (the white-screen the live
        // designer showed). The designer shows the settled content instead, the way the WPF designer does.
        var animate = ContentTransition != ContentTransition.None && newContent != null
                      && !Design.IsDesignMode;

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
        _currentTemplate = null;

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
                _currentTemplate = dataTemplate;   // remember it so a recycled rebind to the same template reuses this visual
            }
            else
            {
                // The auto-generated content text aligns to the PRESENTER's own alignment - which a control template binds
                // from the control's Horizontal/VerticalContentAlignment (e.g. <ContentPresenter VerticalAlignment=
                // "{TemplateBinding VerticalContentAlignment}"/>). So content lands where the template asked, instead of
                // the TextBlock's default (VerticalTextAlignment.Bottom, which looked low). Authored TextBlocks are unaffected.
                var textBlock = new TextBlock
                {
                    Text = newContent.ToString(),
                    FontSize = FontSize,
                    HorizontalTextAlignment = ToTextAlignment(HorizontalAlignment),
                    VerticalTextAlignment = ToTextAlignment(VerticalAlignment)
                };
                if (Foreground != null) textBlock.Foreground = Foreground;
                _currentRoot = textBlock;
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

    public Brush Foreground
    {
        get => GetValue<Brush>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Double FontSize
    {
        get => GetValue<Double>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    // Pushes the (template-bound) text style onto the already-generated TextBlock, so a trigger changing Foreground or
    // FontSize (Pressed/Disabled states) updates the text live, not only on the next content rebuild.
    private static void OnTextStyleChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ContentPresenter presenter) presenter.ApplyTextStyle();
    }

    private void ApplyTextStyle()
    {
        if (_currentRoot is not TextBlock textBlock) return;
        textBlock.FontSize = FontSize;
        if (Foreground != null) textBlock.Foreground = Foreground;
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

    // Map the presenter's own content alignment onto the auto-generated text's alignment, so string content honours the
    // template's Horizontal/VerticalContentAlignment. Stretch has no text equivalent -> left / centre (sensible defaults).
    private static HorizontalTextAlignment ToTextAlignment(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => HorizontalTextAlignment.Center,
        HorizontalAlignment.Right => HorizontalTextAlignment.Right,
        _ => HorizontalTextAlignment.Left,
    };

    private static VerticalTextAlignment ToTextAlignment(VerticalAlignment alignment) => alignment switch
    {
        VerticalAlignment.Top => VerticalTextAlignment.Top,
        VerticalAlignment.Bottom => VerticalTextAlignment.Bottom,
        _ => VerticalTextAlignment.Center,
    };
}
