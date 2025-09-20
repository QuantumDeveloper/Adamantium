using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

public class ContentPresenter : InputUIComponent
{
    private TemplateResult _previousTemplateResult;
    private TemplateResult _currentTemplateResult;
    private IUIComponent _currentContent;
    private bool _isContentChanged;
    
    public static readonly AdamantiumProperty ContentProperty = AdamantiumProperty.Register(nameof(Content),
        typeof(object), typeof(ContentPresenter), new PropertyMetadata(null, OnContentPropertyChanged));
    
    public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
        typeof(DataTemplate), typeof(ContentPresenter), new PropertyMetadata(null, OnContentTemplateChanged));
    
    public static readonly AdamantiumProperty ContentTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ContentTemplateSelector),
        typeof(DataTemplateSelector), typeof(ContentPresenter), new PropertyMetadata(null, OnContentTemplateSelectorChanged));

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
        
        if (_currentContent != null)
        {
            RemoveVisualChild(_currentContent);
            RemoveLogicalChild(_currentContent);

            _currentContent = null;
        }
        
        if (_previousTemplateResult != null)
        {
            RemoveVisualChild(_currentTemplateResult.RootComponent);
            RemoveLogicalChild(_currentTemplateResult.RootComponent);
            
            _previousTemplateResult.Destroy();
            _previousTemplateResult = null;
        }
        
        if (newContent == null)
            return;

        if (newContent is IUIComponent iuiComponent)
        {
            _currentContent = iuiComponent;
            AddVisualChild(_currentTemplateResult.RootComponent);
            AddLogicalChild(_currentTemplateResult.RootComponent);
        }
        else
        {
            var dataTemplate = ContentTemplate;
            if (dataTemplate == null)
            {
                dataTemplate = ContentTemplateSelector?.SelectTemplate(newContent, this);
            }

            if (dataTemplate != null)
            {
                _currentTemplateResult = dataTemplate?.Build(this);
                if (_currentTemplateResult != null)
                {
                    AddVisualChild(_currentTemplateResult.RootComponent);
                    AddLogicalChild(_currentTemplateResult.RootComponent);
                }
            }
            else
            {
                var textBlock = new TextBlock() {Text = newContent.ToString(), FontSize = 28};
                _currentContent = textBlock;
                AddVisualChild(_currentContent);
                AddLogicalChild(_currentContent);
            }
        }
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
    
    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateVisualContent(Content);
        var child = GetVisualDescendants().FirstOrDefault();
        if (child is IMeasurableComponent measurable)
        {
            measurable.Measure(availableSize);
            return measurable.DesiredSize;
        }

        return new Size(0, 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // var child = GetVisualDescendants().FirstOrDefault();
        // if (child is IMeasurableComponent measurable)
        // {
        //     measurable.Arrange(new Rect(finalSize));
        //     return measurable.DesiredSize;
        // }
        //
        // return new Size(0, 0);
        var size = base.ArrangeOverride(finalSize);
        return size;
    }
}