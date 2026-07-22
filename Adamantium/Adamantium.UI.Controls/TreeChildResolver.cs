using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Adamantium.UI.Controls;

/// <summary>Resolves a node's child collection WITHOUT a container, by reflecting a <c>HierarchicalDataTemplate.ItemsSource</c>
/// path (e.g. <c>{Binding Children}</c> → the <c>Children</c> property) against the node. The flattener needs each node's
/// children to project the tree flat, but only the viewport gets containers - so the children can't come off a generated
/// child ItemsControl the way the nested tree did. A compiled getter is cached per (type, segment), mirroring
/// <c>BindingExpression</c>'s accessor cache, so resolving thousands of siblings' children is a dictionary hit + a
/// near-native call, not a reflection invoke each time. Dotted paths (<c>A.B</c>) are walked segment by segment.</summary>
internal static class TreeChildResolver
{
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object>> Getters = new();

    /// <summary>A getter that reads <paramref name="path"/> off a node and returns it as an <see cref="IEnumerable"/>
    /// (null when the path is empty, unresolved, or the value isn't enumerable).</summary>
    public static Func<object, IEnumerable> ForPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return static _ => null;
        }

        var segments = path.Split('.');
        return node => Resolve(node, segments) as IEnumerable;
    }

    /// <summary>A getter that reads a <see cref="bool"/> <paramref name="path"/> off a node (e.g. the node's own
    /// <c>IsExpanded</c>, so the flattener can RESTORE a node's expansion when the tree is rebuilt - after a tab switch the
    /// view is recreated but the view-model, and its expanded state, persist). Always false for an empty/unresolved path.</summary>
    public static Func<object, bool> ForBoolPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return static _ => false;
        }

        var segments = path.Split('.');
        return node => Resolve(node, segments) is true;
    }

    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> Props = new();

    /// <summary>A writer for a <see cref="bool"/> <paramref name="path"/> on a node (e.g. the node's <c>IsSelected</c>), so
    /// the TreeView can persist a selection onto the node itself - INCLUDING off-screen rows that have no container to carry
    /// a binding. No-op for an empty/unresolved path. Not a hot path (selection changes on click), so a plain reflected set.</summary>
    public static Action<object, bool> SetterForBoolPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return static (_, _) => { };
        }

        var segments = path.Split('.');
        return (node, value) =>
        {
            var target = node;
            for (var i = 0; i < segments.Length - 1 && target != null; i++)
            {
                target = Getter(target.GetType(), segments[i])?.Invoke(target);
            }

            if (target == null)
            {
                return;
            }

            var prop = Props.GetOrAdd((target.GetType(), segments[^1]), static k => k.Item1.GetProperty(k.Item2));
            if (prop is { CanWrite: true })
            {
                prop.SetValue(target, value);
            }
        };
    }

    private static object Resolve(object node, string[] segments)
    {
        var current = node;
        foreach (var segment in segments)
        {
            if (current == null)
            {
                return null;
            }

            current = Getter(current.GetType(), segment)?.Invoke(current);
        }

        return current;
    }

    private static Func<object, object> Getter(Type type, string name)
        => Getters.GetOrAdd((type, name), static key =>
        {
            var prop = key.Item1.GetProperty(key.Item2);
            if (prop is not { CanRead: true })
            {
                return null;
            }

            var o = Expression.Parameter(typeof(object), "o");
            var body = Expression.Convert(Expression.Property(Expression.Convert(o, key.Item1), prop), typeof(object));
            return Expression.Lambda<Func<object, object>>(body, o).Compile();
        });
}
