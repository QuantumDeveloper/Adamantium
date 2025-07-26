using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Markup.CodeGeneration.Roslyn;

public class RoslynOutputSink(SourceProductionContext context) : ICodeOutputSink
{
    private readonly SourceProductionContext _context = context;

    public void Emit(string hintName, string code)
    {
        _context.AddSource($"{hintName}.g.cs", code);
    }
}