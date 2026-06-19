namespace Adamantium.Content.Pipeline;

/// <summary>
/// Optional transform of the imported intermediate (optimization, generation, etc.) before it is written.
/// Model processing currently lives inside the importer, so this is reserved for future asset types.
/// </summary>
public interface IContentProcessor
{
    object Process(object input, ContentBuildContext context);
}
