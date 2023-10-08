using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Templates;

namespace Adamantium.UI.Controls;

public class Control : InputUIComponent, IControl
{
   private TemplateResult templateResult;
   public Control()
   {

   }
   
   public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
      typeof(Brush), typeof(Control),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty ForegroundProperty = AdamantiumProperty.Register(nameof(Foreground),
      typeof(Brush), typeof(Control),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty TemplateProperty =
      AdamantiumProperty.Register(nameof(Template), typeof(ControlTemplate), typeof(MeasurableUIComponent),
         new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, TemplateChangedCallback));

   private static void TemplateChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      if (a is Control component)
      {
         if (e.OldValue is ControlTemplate oldTemplate)
         {
            component.RemoveTemplate();
         }

         if (e.NewValue is ControlTemplate newTemplate)
         {
            component.ApplyTemplate();
         }
      }
   }

   public ControlTemplate Template
   {
      get => GetValue<ControlTemplate>(TemplateProperty);
      set => SetValue(TemplateProperty, value);
   }

   public Brush Background
   {
      get => GetValue<Brush>(BackgroundProperty);
      set => SetValue(BackgroundProperty, value);
   }

   public Brush Foreground
   {
      get => GetValue<Brush>(ForegroundProperty);
      set => SetValue(ForegroundProperty, value);
   }
   
   public IAdamantiumComponent GetTemplateChild(string name)
   {
      if (Template == null || templateResult?.RootComponent == null) return null;

      return templateResult.GetComponentByName(name);
   }

   private void ApplyTemplate()
   {
      if (Template == null) return;
      
      templateResult = Template.Build();
      AddVisualChild(templateResult.RootComponent);
      OnApplyTemplate();
   }

   private void RemoveTemplate()
   {
      templateResult = null;
      RemoveVisualChildren();
      OnRemoveTemplate();
   }

   public virtual void OnRemoveTemplate()
   {
      RaiseEvent(new RoutedEventArgs(UnloadedEvent, this));
   }

   public virtual void OnApplyTemplate()
   {
   }
}