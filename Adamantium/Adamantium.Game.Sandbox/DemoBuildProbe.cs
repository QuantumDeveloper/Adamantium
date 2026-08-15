using System;
using Adamantium.UI.Controls.Decorators;

namespace Adamantium.Game.Sandbox;

/// <summary>A Border that says when it was CONSTRUCTED. The x:Load demo has no other way to make its claim checkable:
/// "not built until asked, and built only once" is a statement about construction, and nothing in the tree reports
/// that on its own. Reports its own type, so the two arms of the demo count separately.</summary>
public class DemoBuildProbe : Border
{
    public static event Action<Type> Built;

    public DemoBuildProbe()
    {
        Built?.Invoke(GetType());
    }
}
