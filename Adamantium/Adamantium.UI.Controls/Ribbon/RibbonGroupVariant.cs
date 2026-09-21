using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.UI.Controls;

/// <summary>One way a <see cref="RibbonGroup"/> can be drawn: the size of every command in it, or the group reduced to a
/// single button. Roomiest first; the panel takes the first that fits. Derived from what the commands allow rather than
/// written out as a matrix - writing that matrix by hand is the main complaint against the WPF ribbon.</summary>
public sealed class RibbonGroupVariant
{
    private RibbonGroupVariant(RibbonSize[] sizes, bool isCollapsed)
    {
        Sizes = sizes;
        IsCollapsed = isCollapsed;
    }

    /// <summary>The size of each command, in the order the commands were declared.</summary>
    public IReadOnlyList<RibbonSize> Sizes { get; }

    /// <summary>The group is one button; its commands live in the flyout it opens.</summary>
    public bool IsCollapsed { get; }

    /// <summary>Roomiest first, collapsed last. The GROUP steps as one - as asked, then medium, then small - and each
    /// command follows or sits a step out by its thresholds. Stepping them individually reads as broken: a labelled row
    /// ends up stacked over a bare icon.</summary>
    public static IReadOnlyList<RibbonGroupVariant> Generate(
        IReadOnlyList<(RibbonSize Max, RibbonCollapseThreshold ToMedium, RibbonCollapseThreshold ToSmall)> commands)
    {
        var variants = new List<RibbonGroupVariant>();

        // Three steps, because the group has three sizes to be: as asked, medium, small. A step that changes nothing -
        // every command sat it out - is not offered, so a group of fixed-size commands still has exactly one layout.
        for (var step = 0; step < 3; step++)
        {
            var sizes = new RibbonSize[commands.Count];
            for (var i = 0; i < commands.Count; i++) sizes[i] = SizeAt(commands[i], step);

            if (variants.Count > 0 && Same(variants[^1].Sizes, sizes)) continue;
            variants.Add(new RibbonGroupVariant(sizes, false));
        }

        variants.Add(new RibbonGroupVariant([], true));
        return variants;
    }

    private static RibbonSize SizeAt((RibbonSize Max, RibbonCollapseThreshold ToMedium, RibbonCollapseThreshold ToSmall) command, int step)
    {
        var size = command.Max;
        // Never smaller than asked for: a threshold reached by a command already drawn small must not grow it back.
        if (Reached(command.ToMedium, step) && size < RibbonSize.Medium) size = RibbonSize.Medium;
        if (Reached(command.ToSmall, step)) size = RibbonSize.Small;

        return size;
    }

    private static bool Reached(RibbonCollapseThreshold threshold, int step) => threshold switch
    {
        RibbonCollapseThreshold.WhenGroupIsMedium => step >= 1,
        RibbonCollapseThreshold.WhenGroupIsSmall => step >= 2,
        _ => false
    };

    private static bool Same(IReadOnlyList<RibbonSize> a, IReadOnlyList<RibbonSize> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }

        return true;
    }
}
