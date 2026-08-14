namespace Adamantium.UI.Core.Resources;

/// <summary>
/// A setter value that is BUILT PER TARGET instead of shared between them - what <c>x:Shared="False"</c> asks for.
/// <para>A setter otherwise writes the same reference into every element it matches, so nothing that must be personal can
/// live in a theme at all: a <c>ContextMenu</c> has one <c>PlacementTarget</c>, a <c>Popup</c> one placement, a
/// <c>Transform</c> one owner. The only way out was a template, and a template fits only where the property is typed for
/// one - so such values were written in C# instead of stated in the theme.</para>
/// </summary>
public sealed class PerTargetValue
{
    private readonly Func<object> _factory;

    public PerTargetValue(Func<object> factory)
    {
        _factory = factory;
    }

    /// <summary>Builds a value for one target. Called once per element the setter applies to.</summary>
    public object Create() => _factory?.Invoke();
}
