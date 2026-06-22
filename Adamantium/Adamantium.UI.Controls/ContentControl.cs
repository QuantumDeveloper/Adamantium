using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

public class ContentControl : Control, IContentControl
{
   private IUIComponent _currentVisualChild;
   private IUIComponent _outgoingVisualChild;
   private bool _transitionPending;

   private ContentPresenter _contentPresenter;
   private bool _contentChanged;

   public static readonly AdamantiumProperty ContentProperty = AdamantiumProperty.Register(nameof(Content),
      typeof(object), typeof(ContentControl), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, ContentChangedCallback));
   
   public static readonly AdamantiumProperty HorizontalContentAlignmentProperty = AdamantiumProperty.Register(nameof(HorizontalContentAlignment),
      typeof(HorizontalAlignment), typeof(MeasurableUIComponent), new PropertyMetadata(HorizontalAlignment.Stretch, PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty VerticalContentAlignmentProperty = AdamantiumProperty.Register(nameof(VerticalContentAlignment),
      typeof(VerticalAlignment), typeof(MeasurableUIComponent), new PropertyMetadata(VerticalAlignment.Stretch, PropertyMetadataOptions.AffectsArrange));
   
   public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
      typeof(DataTemplate), typeof(ContentControl), new PropertyMetadata(null, OnContentTemplateChanged));
    
   public static readonly AdamantiumProperty ContentTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ContentTemplateSelector),
      typeof(DataTemplateSelector), typeof(ContentControl), new PropertyMetadata(null, OnContentTemplateSelectorChanged));

   public static readonly AdamantiumProperty ContentTransitionProperty = AdamantiumProperty.Register(nameof(ContentTransition),
      typeof(ContentTransition), typeof(ContentControl), new PropertyMetadata(ContentTransition.None));

   public static readonly AdamantiumProperty TransitionDurationProperty = AdamantiumProperty.Register(nameof(TransitionDuration),
      typeof(Double), typeof(ContentControl), new PropertyMetadata(0.25));

   private static void ContentChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is ContentControl o)
      {
         o.OnContentChangedInternal(e.OldValue, e.NewValue);
      }
   }
    
   private static void OnContentTemplateChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is ContentControl o)
      {
         o.OnContentChangedInternal(e.OldValue, e.NewValue);
      }
   }
    
   private static void OnContentTemplateSelectorChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is ContentControl o)
      {
         o.OnContentChangedInternal(e.OldValue, e.NewValue);
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
   
   public VerticalAlignment VerticalContentAlignment
   {
      get => GetValue<VerticalAlignment>(VerticalContentAlignmentProperty);
      set => SetValue(VerticalContentAlignmentProperty, value);
   }

   public HorizontalAlignment HorizontalContentAlignment
   {
      get => GetValue<HorizontalAlignment>(HorizontalContentAlignmentProperty);
      set => SetValue(HorizontalContentAlignmentProperty, value);
   }

   /// <summary>The animation played when <see cref="Content"/> is replaced. Defaults to <see cref="ContentTransition.None"/>.</summary>
   public ContentTransition ContentTransition
   {
      get => GetValue<ContentTransition>(ContentTransitionProperty);
      set => SetValue(ContentTransitionProperty, value);
   }

   /// <summary>Duration of the content transition, in seconds. Defaults to 0.25.</summary>
   public Double TransitionDuration
   {
      get => GetValue<Double>(TransitionDurationProperty);
      set => SetValue(TransitionDurationProperty, value);
   }

   public override void OnApplyTemplate()
   {
      base.OnApplyTemplate();
      _contentPresenter = (ContentPresenter)GetTemplateChild("PART_ContentPresenter");
      _contentChanged = true;
   }

   private void OnContentChangedInternal(object oldContent, object newContent)
   {
      _contentChanged = true;
      OnContentChanged(oldContent, newContent);
   }

   protected virtual void OnContentChanged(object oldContent, object newContent)
   {
      
   }
   
   private void UpdateVisualsForContent(object content)
   {
      if (!_contentChanged)
         return;
      // Mark the change consumed up-front so a clean measure does not re-run the detach/re-attach below.
      // A later Content/Template change sets _contentChanged = true again (OnContentChangedInternal / OnApplyTemplate).
      _contentChanged = false;

      // A new swap supersedes one still mid-flight: finish the previous transition instantly first.
      RemoveOutgoing();

      // Templated path: PART_ContentPresenter hosts the content (and runs its own transition). Drop any direct child.
      if (Template != null)
      {
         if (_currentVisualChild != null)
         {
            RemoveVisualChild(_currentVisualChild);
            RemoveLogicalChild(_currentVisualChild);
            _currentVisualChild = null;
         }
         return;
      }

      // Animate when a transition is selected and there is new content - including the FIRST content (slides in from
      // the side and settles, MahApps-style). In the designer it plays only for the LIVE previewer (frame stream); a
      // one-shot render shows the settled state instead (like the WPF designer), since it would catch a mid-slide frame.
      var animate = ContentTransition != ContentTransition.None && content != null
                    && (!Design.IsDesignMode || Design.IsLivePreview);

      if (animate && _currentVisualChild != null)
      {
         _outgoingVisualChild = _currentVisualChild;   // keep the old child to slide it out
      }
      else if (_currentVisualChild != null)
      {
         RemoveVisualChild(_currentVisualChild);
         RemoveLogicalChild(_currentVisualChild);
      }
      _currentVisualChild = null;

      if (content != null)
      {
         _currentVisualChild = content as IUIComponent ?? new TextBlock { FontSize = 28, Text = content.ToString() };
         AddVisualChild(_currentVisualChild);
         AddLogicalChild(_currentVisualChild);
      }

      _transitionPending = animate && _currentVisualChild != null;
   }

   private void RemoveOutgoing()
   {
      if (_outgoingVisualChild == null)
         return;
      RemoveVisualChild(_outgoingVisualChild);
      RemoveLogicalChild(_outgoingVisualChild);
      _outgoingVisualChild = null;
   }

   protected override Size MeasureOverride(Size availableSize)
   {
      UpdateVisualsForContent(Content);
      
      return base.MeasureOverride(availableSize);
      
      // var visual = VisualChildrenCollection.FirstOrDefault();
      // if (visual is IMeasurableComponent child)
      // {
      //    child.Measure(availableSize);
      //    return child.DesiredSize;
      // }
      //
      // return new Size(0, 0);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      // var visual = VisualChildrenCollection.FirstOrDefault();
      // if (visual is IMeasurableComponent child)
      // {
      //    child.Arrange(new Rect(finalSize));
      //    return child.Bounds.Size;
      // }
      
      foreach (var visual in VisualChildren)
      {
         var child = (IMeasurableComponent)visual;
            
         var childRect = CalculateChildArrangeRect(
            finalSize, 
            child.DesiredSize, 
            HorizontalContentAlignment,
            VerticalContentAlignment);
                
         child.Arrange(childRect);
      }

      if (_transitionPending)
      {
         // Start the slide now that both children are arranged at their final rects (size known here). Sliding content
         // can extend past the control while it moves; clip it (enforcement depends on renderer clip support).
         _transitionPending = false;
         ClipToBounds = true;
         ContentTransitions.Run(ContentTransition, TransitionDuration, finalSize, _currentVisualChild, _outgoingVisualChild, RemoveOutgoing);
      }

      return finalSize;
   }
   
   protected static Rect CalculateChildArrangeRect(
      Size parentFinalSize,
      Size childDesiredSize,
      HorizontalAlignment horizontalAlignment,
      VerticalAlignment verticalAlignment)
   {
      double x = 0;
      double y = 0;
      double width = childDesiredSize.Width;
      double height = childDesiredSize.Height;
        
      // --- Горизонтальное позиционирование ---
      // (здесь может быть более сложная логика с Margin из вашего ArrangeCore)
      if (horizontalAlignment == HorizontalAlignment.Center)
      {
         x = (parentFinalSize.Width - childDesiredSize.Width) / 2;
      }
      else if (horizontalAlignment == HorizontalAlignment.Right)
      {
         x = parentFinalSize.Width - childDesiredSize.Width;
      }
      else if (horizontalAlignment == HorizontalAlignment.Stretch)
      {
         width = parentFinalSize.Width;
      }

      // --- Вертикальное позиционирование ---
      if (verticalAlignment == VerticalAlignment.Center)
      {
         y = (parentFinalSize.Height - childDesiredSize.Height) / 2;
      }
      else if (verticalAlignment == VerticalAlignment.Bottom)
      {
         y = parentFinalSize.Height - childDesiredSize.Height;
      }
      else if (verticalAlignment == VerticalAlignment.Stretch)
      {
         height = parentFinalSize.Height;
      }

      return new Rect(x, y, width, height);
   }


   void IContainer.AddOrSetChildComponent(object component)
   {
      Content = component;
   }

   void IContainer.RemoveAllChildComponents()
   {
      Content = null;
   }
   
   protected override void OnRender(IDrawingContext context)
   {
      var size = new Size(ActualWidth, ActualHeight);

      context.ForControl(this)
         .DrawRectangle(Background, new Rect(size));

   }
}