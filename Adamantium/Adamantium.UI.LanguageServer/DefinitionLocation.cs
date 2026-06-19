namespace Adamantium.UI.LanguageServer;

/// <summary>A navigable declaration location for go-to-definition: a file path and a 0-based range.</summary>
public sealed record DefinitionLocation(string FilePath, int StartLine, int StartCharacter, int EndLine, int EndCharacter);
