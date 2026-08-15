namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One row of the Markup tab's list. Exists so the template above it has a type to NAME with x:DataType - the
/// point of the directive being that the type is stated, not guessed from whatever happens to be in the list.</summary>
public sealed class MarkupRow
{
    public MarkupRow(string title) => Title = title;

    public string Title { get; }
}
