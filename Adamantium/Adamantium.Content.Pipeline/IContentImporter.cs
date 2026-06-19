namespace Adamantium.Content.Pipeline;

/// <summary>
/// Turns a raw source asset file into an engine-neutral intermediate object model
/// (e.g. a model file into <c>SceneData</c>).
/// </summary>
public interface IContentImporter
{
    object Import(string sourcePath, ContentBuildContext context);
}
