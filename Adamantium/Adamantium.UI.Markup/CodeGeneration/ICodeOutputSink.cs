namespace Adamantium.UI.Markup.CodeGeneration;

public interface ICodeOutputSink
{
    void Emit(string hintName, string code);
}