using System;

namespace Adamantium.Content.Pipeline;

/// <summary>
/// Marks a content importer and declares the logical name it is registered under plus the source file
/// extensions it handles (XNA <c>[ContentImporter]</c> analog).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ContentImporterAttribute : Attribute
{
    public ContentImporterAttribute(string name, params string[] extensions)
    {
        Name = name;
        Extensions = extensions ?? [];
    }

    public string Name { get; }

    public string[] Extensions { get; }
}