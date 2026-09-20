using System;
using System.Collections.Generic;
using Adamantium.Core.Collections;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>The sets in one of these, in the order they were written - see <see cref="CanvasInspectorSections"/>.</summary>
public class CanvasSectionSets : TrackingCollection<CanvasSectionSet>
{
}

/// <summary>What a canvas's inspector SHOWS - a list of <see cref="CanvasSectionSet"/>, each saying which kind of thing
/// its lines belong to.
/// <para>The default one ships with the themes as a shared resource; an application declares its own and the canvas
/// lays it over the default - see <see cref="InfiniteCanvas.InspectorSections"/>. So putting something new on the plane
/// costs the lines it is set by, and nothing else: no flag on the panel, no edit to three themes.</para>
/// <para>ORDER is part of what is being said: the sections come out in the order they were written, general before
/// particular, so a rectangle shows what every shape has and then its own corners.</para></summary>
public class CanvasInspectorSections : AdamantiumComponent
{
    /// <summary>The sets. [Content], so one of these is written as the sets it is.</summary>
    [Content]
    public CanvasSectionSets Sets { get; } = new();

    /// <summary>The sections that belong to a thing of these names, in order.
    /// <para>Every set whose <see cref="CanvasSectionSet.For"/> is empty or matches one of the names, which is how a
    /// rectangle gets both what every shape has and what only a rectangle has. The caller says which names it means -
    /// for something on the plane that is its class and its <see cref="ICanvasItem.Sort"/>, for a tool its
    /// <see cref="ICanvasTool.Name"/>.</para></summary>
    public IReadOnlyList<PropertySection> For(IReadOnlyList<string> names)
    {
        var found = new List<PropertySection>();

        foreach (var set in Sets)
        {
            if (set == null || !Matches(set.For, names)) continue;

            foreach (var section in set.Sections) found.Add(section);
        }

        return found;
    }

    /// <summary>This list with another laid OVER it: a set naming something the first already covers replaces it, a set
    /// naming something new is added after.
    /// <para>What lets an application say only its own part. Replacing wholesale is still open to it - it declares a
    /// resource under the default's own key - but the ordinary case is one more kind of thing, and that must not cost a
    /// copy of the panel.</para></summary>
    public CanvasInspectorSections With(CanvasInspectorSections other)
    {
        if (other == null || other.Sets.Count == 0) return this;

        var merged = new CanvasInspectorSections();

        foreach (var set in Sets)
        {
            if (set == null) continue;

            merged.Sets.Add(Named(other, set.For) ?? set);
        }

        foreach (var set in other.Sets)
        {
            if (set == null || Named(this, set.For) != null) continue;

            merged.Sets.Add(set);
        }

        return merged;
    }

    private static CanvasSectionSet Named(CanvasInspectorSections among, string what)
    {
        foreach (var set in among.Sets)
        {
            if (set != null && Same(set.For, what)) return set;
        }

        return null;
    }

    private static bool Matches(string what, IReadOnlyList<string> names)
    {
        if (string.IsNullOrEmpty(what)) return true;
        if (names == null) return false;

        foreach (var name in names)
        {
            if (Same(what, name)) return true;
        }

        return false;
    }

    // BY NAME, ignoring case: these are written by hand in markup, and "image" meaning something else than "Image"
    // would be a difference nobody can see in the file they are looking at.
    private static bool Same(string one, string other) =>
        string.Equals(one ?? string.Empty, other ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
