using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>
/// Where a render mark GOES. The marks themselves are described by <see cref="RenderDirtyScope"/>; this says whose they
/// are.
/// <para>They used to be one process-wide set, which made two things impossible. Two windows shared it, so it could not
/// be cleared after the first of them recorded - the second would then re-record nothing (the comment in
/// RenderCache.ApplyFrame is that compromise). And within one window the three STAGES - the content, the popup/overlay
/// layer, the adorners - shared it too, so a hovered menu item marked the content dirty and the content stage went
/// looking for what changed; the "from a foreign tree, skip it" branch in ClassifyReRender is that same fact, handled
/// one symptom at a time.</para>
/// <para>A component belongs to exactly one stage, so the routing is unambiguous. Until a stage claims a subtree, marks
/// land in <see cref="Default"/>, which is what the single shared set was.</para>
/// </summary>
public static class RenderDirtyRouter
{
    /// <summary>The scope everything belongs to until a stage says otherwise - the window content.</summary>
    public static readonly RenderDirtyScope Default = new();

    // Every scope in existence, for the once-per-frame clear. Scopes are created by stages (a handful per window), never
    // per component, so this stays tiny.
    private static readonly List<RenderDirtyScope> Scopes = new() { Default };

    /// <summary>A scope for a stage that draws a subtree of its own. Registered here so a clear reaches it.</summary>
    public static RenderDirtyScope NewScope()
    {
        var scope = new RenderDirtyScope();
        lock (Scopes) Scopes.Add(scope);
        return scope;
    }

    /// <summary>The scope this component's marks belong to - a field READ, because marking is the hottest path there is:
    /// a scrolling frame marks thousands of components, and asking each of them to walk up to its stage would put a tree
    /// walk into every one of those marks.</summary>
    public static RenderDirtyScope Of(IUIComponent component) => component?.RenderScope ?? Default;

    /// <summary>Every scope there is, for the once-per-frame clear.</summary>
    public static IReadOnlyList<RenderDirtyScope> All()
    {
        lock (Scopes) return Scopes.ToArray();
    }
}
