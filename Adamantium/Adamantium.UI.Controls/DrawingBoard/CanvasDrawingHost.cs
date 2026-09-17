using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Keeps what the application says is ON THE PLANE in step with what draws it, in both directions - the
/// drawing's <see cref="CanvasGraphHost"/>.
/// <para>THE CANVAS MAKES ITS OWN TYPES. The application hands over data - where a thing is and what it is - and never
/// a scene item, and never a control: a shape described here becomes the canvas's own drawing of it, and anything else
/// becomes a control the canvas builds from the template chosen for that data's type.</para></summary>
internal sealed class CanvasDrawingHost
{
    private readonly Dictionary<ICanvasObject, ICanvasItem> _placed = new();

    private IEnumerable _objects;
    private ICanvasScene _scene;
    private Adamantium.UI.Core.Templates.DataTemplateSelector _selector;

    /// <summary>What draws the ones that are not shapes. Handed to each control as it is made, and to those already
    /// standing when it arrives.</summary>
    public void SetTemplateSelector(Adamantium.UI.Core.Templates.DataTemplateSelector selector)
    {
        if (ReferenceEquals(_selector, selector)) return;

        _selector = selector;

        foreach (var (_, item) in _placed)
        {
            if (item is ElementItem { Element: ContentPresenter presenter })
            {
                presenter.ContentTemplateSelector = selector;
            }
        }
    }

    /// <summary>The application's collection. Followed while it is set, so one added to it appears on the plane and one
    /// taken out of it leaves.</summary>
    public void SetObjects(IEnumerable objects)
    {
        if (ReferenceEquals(_objects, objects)) return;

        if (_objects is INotifyCollectionChanged was) was.CollectionChanged -= OnObjectsChanged;

        Clear();
        _objects = objects;

        if (_objects is INotifyCollectionChanged now) now.CollectionChanged += OnObjectsChanged;

        Fill();
    }

    public void SetScene(ICanvasScene scene)
    {
        if (ReferenceEquals(_scene, scene)) return;

        _scene = scene;
        Fill();
    }

    private void OnObjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Clear();
            Fill();
            return;
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is ICanvasObject model) Drop(model);
            }
        }

        if (e.NewItems == null) return;

        foreach (var item in e.NewItems)
        {
            if (item is ICanvasObject model) Put(model);
        }
    }

    private void Fill()
    {
        if (_objects == null || _scene == null) return;

        foreach (var item in _objects)
        {
            if (item is ICanvasObject model) Put(model);
        }
    }

    private void Put(ICanvasObject model)
    {
        if (_scene == null || _placed.ContainsKey(model)) return;

        var item = Make(model);
        if (item == null) return;

        _placed[model] = item;
        model.PropertyChanged += OnModelChanged;

        _scene.Add(item);
    }

    // WHAT DRAWS IT, decided by what the application says it is. A shape the canvas knows how to draw; anything else is
    // the application's own object, and a control built here around it - the template says what that control looks
    // like, and the application never touches one.
    private ICanvasItem Make(ICanvasObject model)
    {
        var box = new Rect(model.Left, model.Top, model.Width, model.Height);

        if (model.Content is ICanvasShapeDescription shape)
        {
            return new ShapeItem(shape.Shape, box, shape.Stroke, shape.Thickness, shape.Fill)
            {
                Corner = shape.Corner,
                Sides = shape.Sides,
                StartHead = shape.StartHead,
                EndHead = shape.EndHead,
                Model = model
            };
        }

        var presenter = new ContentPresenter
        {
            Content = model.Content,
            ContentTemplateSelector = _selector
        };

        return new ElementItem(presenter, box) { Model = model };
    }

    private void Drop(ICanvasObject model)
    {
        if (!_placed.Remove(model, out var item)) return;

        model.PropertyChanged -= OnModelChanged;
        _scene?.Remove(item);
    }

    private void Clear()
    {
        foreach (var (model, item) in _placed)
        {
            model.PropertyChanged -= OnModelChanged;
            _scene?.Remove(item);
        }

        _placed.Clear();
    }

    // Nothing is copied: the box a shape occupies IS the object's, read and written through. The scene is told that a
    // rectangle on it moved, because the plane is drawn from the items and nobody else would ask it to do that again.
    private void OnModelChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ICanvasObject.Left):
            case nameof(ICanvasObject.Top):
            case nameof(ICanvasObject.Width):
            case nameof(ICanvasObject.Height):
                (_scene as CanvasScene)?.Touch();
                break;
        }
    }
}
