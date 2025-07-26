using Adamantium.Core;

namespace Adamantium.UI.Markup.CodeGeneration;

public class CodeGenerationContext
{
    public string ParentName { get; set; }
    public Stack<string> ElementStack { get; } = new();
    public TextGenerator Generator { get; }
    public AumlMetadataContainer Metadata { get; }
    public int Id { get; set; }

    public CodeGenerationContext(TextGenerator generator, AumlMetadataContainer metadata)
    {
        Generator = generator;
        Metadata = metadata;
    }

    public string GetNextElementName()
    {
        return $"element_{Id++}";
    }

    public void Push(string name)
    {
        ElementStack.Push(name);
        ParentName = name;
    }

    public void Pop()
    {
        ElementStack.Pop();
        ParentName = ElementStack.Count > 0 ? ElementStack.Peek() : string.Empty;
    }
}