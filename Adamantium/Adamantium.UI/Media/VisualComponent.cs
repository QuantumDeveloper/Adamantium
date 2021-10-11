using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Media
{
   public class VisualComponent: AdamantiumComponent, IVisualComponent
   {
      public static readonly AdamantiumProperty PositionProperty = AdamantiumProperty.Register(nameof(Location),
         typeof (Point), typeof (VisualComponent), new PropertyMetadata(Point.Zero));

      public static readonly AdamantiumProperty RotationProperty = AdamantiumProperty.Register(nameof(Rotation),
         typeof(Double), typeof(VisualComponent), new PropertyMetadata((Double)0));

      public static readonly AdamantiumProperty ScaleProperty = AdamantiumProperty.Register(nameof(Scale),
         typeof(Vector2D), typeof(VisualComponent), new PropertyMetadata(Vector2D.One));

      public static readonly AdamantiumProperty VisibilityProperty = AdamantiumProperty.Register(nameof(Visibility),
         typeof(Visibility), typeof(VisualComponent),
         new PropertyMetadata(Visibility.Visible,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsArrange));
      
      public Point Location
      {
         get => GetValue<Point>(PositionProperty);
         set => SetValue(PositionProperty, value);
      }

      public Vector2D Scale
      {
         get => GetValue<Vector2D>(ScaleProperty);
         set => SetValue(ScaleProperty, value);
      }

      public Double Rotation
      {
         get => GetValue<Double>(RotationProperty);
         set => SetValue(RotationProperty, value);
      }

      public Visibility Visibility
      {
         get => GetValue<Visibility>(VisibilityProperty);
         set => SetValue(VisibilityProperty, value);
      }

      public VisualComponent()
      {
         VisualChildren = new TrackingCollection<IVisualComponent>();
         VisualChildren.CollectionChanged += VisualChildrenCollectionChanged;
      }

      private void VisualChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
      {
         switch (e.Action)
         {
               case NotifyCollectionChangedAction.Add:
               foreach (VisualComponent visual in e.NewItems)
               {
                  visual.SetVisualParent(this);
               }
               break;

               case NotifyCollectionChangedAction.Remove:
               foreach (VisualComponent visual in e.OldItems)
               {
                  visual.SetVisualParent(null);
               }
               break;
         }
      }

      public Rect Bounds { get; set; }

      public Rect ClipRectangle { get; internal set; }

      public Point ClipPosition { get; set; }

      private VisualComponent visualComponentParent;

      public IVisualComponent VisualComponentParent => visualComponentParent;

      public Int32 ZIndex { get; set; }

      ReadOnlyCollection<IVisualComponent> IVisualComponent.VisualChildren => VisualChildren.AsReadOnly();

      protected TrackingCollection<IVisualComponent> VisualChildren { get; private set; } 
      
      public bool IsAttachedToVisualTree { get; private set; }

      protected void SetVisualParent(VisualComponent parent)
      {
         if (visualComponentParent == parent)
         {
            return;
         }

         var old = visualComponentParent;
         visualComponentParent = parent;

         if (IsAttachedToVisualTree)
         {
            var root = (this as IRootVisualComponent) ?? old.GetSelfAndVisualAncestors().OfType<IRootVisualComponent>().FirstOrDefault();
            var e = new VisualTreeAttachmentEventArgs(root);
            DetachedFromVisualTree(e);
         }

         if (visualComponentParent is IRootVisualComponent || visualComponentParent?.IsAttachedToVisualTree == true)
         {
            var root =  this.GetVisualAncestors().OfType<IRootVisualComponent>().FirstOrDefault();
            var e = new VisualTreeAttachmentEventArgs(root);
            AttachedToVisualTree(e);
         }

         OnVisualParentChanged(old, parent);
      }

      private void AttachedToVisualTree(VisualTreeAttachmentEventArgs e)
      {
         IsAttachedToVisualTree = true;

         OnAttachedToVisualTree(e);

         if (VisualChildren.Count > 0)
         {
            foreach (VisualComponent visualChild in VisualChildren)
            {
               visualChild.AttachedToVisualTree(e);
            }
         }
      }

      private void DetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
      {
         IsAttachedToVisualTree = false;

         OnDetachedFromVisualTree(e);

         if (VisualChildren.Count > 0)
         {
            foreach (var visual in VisualChildren)
            {
                var visualChild = (VisualComponent) visual;
                visualChild.DetachedFromVisualTree(e);
            }
         }
      }

      protected virtual void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
      {
         
      }

      protected virtual void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
      {
         
      }

      protected void OnVisualParentChanged(VisualComponent oldParent, VisualComponent newParent)
      {
         VisualParentChanged?.Invoke(this, new VisualParentChangedEventArgs(oldParent, newParent));
      }

      public EventHandler<VisualParentChangedEventArgs> VisualParentChanged; 

      public virtual void OnRender(DrawingContext context)
      {
      }
   }
}
