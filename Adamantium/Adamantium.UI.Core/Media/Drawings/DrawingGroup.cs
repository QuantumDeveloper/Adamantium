using System.Collections.Specialized;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media.Drawings;

/// <summary>Composition: several drawings painted in order as one, optionally placed by a <see cref="Transform"/>. This
/// is what makes a multi-part vector icon a single reusable resource.</summary>
public class DrawingGroup : Drawing
{
    public DrawingGroup()
    {
        Children = [];
        Children.CollectionChanged += OnChildrenChanged;
    }

    public static readonly AdamantiumProperty TransformProperty = AdamantiumProperty.Register(nameof(Transform),
        typeof(Transform), typeof(DrawingGroup), new PropertyMetadata(null, TransformChangedCallback));

    /// <summary>Places the whole group inside its parent's coordinates. Folded into the matrix handed down at replay, so
    /// it never touches the child geometry - the same geometry stays one shared mesh however many groups place it.</summary>
    public Transform Transform
    {
        get => GetValue<Transform>(TransformProperty);
        set => SetValue(TransformProperty, value);
    }

    /// <summary>The drawings, painted first to last. [Content] so AUML fills it from the group's child elements.</summary>
    [Content]
    public DrawingCollection Children { get; }

    private static void TransformChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not DrawingGroup group) return;

        if (e.OldValue is Transform oldTransform)
        {
            oldTransform.PropertyChanged -= group.OnTransformChanged;
        }

        if (e.NewValue is Transform newTransform)
        {
            newTransform.PropertyChanged += group.OnTransformChanged;
        }
    }

    private void OnTransformChanged(object sender, AdamantiumPropertyChangedEventArgs e) => RaiseChanged();

    private void OnChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (Drawing child in e.OldItems)
            {
                child.Changed -= OnChildChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (Drawing child in e.NewItems)
            {
                child.Changed += OnChildChanged;
            }
        }

        RaiseChanged();
    }

    protected override void AttachChildren()
    {
        AttachOwned(Transform);

        foreach (var child in Children)
        {
            child.Attach(this);
        }
    }

    /// <summary>The union of the children's bounds, through this group's transform.</summary>
    public override Rect Bounds
    {
        get
        {
            var transform = Transform;
            var bounds = Rect.Empty;
            var first = true;

            foreach (var child in Children)
            {
                var childBounds = child.Bounds;
                if (childBounds.IsEmpty) continue;

                // Transform EACH child, then merge - not merge then transform. The union of axis-aligned boxes is
                // bigger than the shapes in it, and taking the AABB of THAT under a rotation inflates it again: a
                // rotating group grew past the box its siblings define, the viewbox grew with it, and the whole picture
                // shrank and sprang back as the angle went round.
                if (transform != null) childBounds = childBounds.TransformToAABB(transform.Matrix);

                bounds = first ? childBounds : bounds.Merge(childBounds);
                first = false;
            }

            return first ? Rect.Empty : bounds;
        }
    }

    public override void Render(IDrawingSession session, Matrix4x4F transform)
    {
        var own = Transform;
        // The group's own placement happens FIRST, in its parent's coordinates, so it pre-multiplies what came down.
        var combined = own == null ? transform : (Matrix4x4F)own.Matrix * transform;

        foreach (var child in Children)
        {
            child.Render(session, combined);
        }
    }
}
