using System;

namespace Adamantium.UI.Resources;

[AttributeUsage(AttributeTargets.Class)]
public class StyleRepositoryItemAttribute : Attribute
{
    public string Path { get; }

    public StyleRepositoryItemAttribute(string path)
    {
        Path = path;
    }
}