using System.Reflection;
using Adamantium.UI.Markup.CodeGeneration;
using Adamantium.UI.Markup.CodeGeneration.Reflection;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Core.Markup;

/// <summary>
/// Runtime AUML loader: parses markup, resolves it through the engine's own <c>DefaultAumlTransformer</c>
/// (backed by a <see cref="ReflectionTypeResolver"/> over the real loaded assemblies), then instantiates a
/// live object tree by reflection. Used by the live designer to turn an editor buffer into a renderable tree
/// without compiling.
/// </summary>
public static class AumlLoader
{
    /// <param name="typeMapper">Optional substitution applied to every resolved type before instantiation
    /// (e.g. map a root <c>IWindow</c> to a headless virtual window). Returns the type to instantiate.</param>
    public static AumlLoadResult Load(string aumlText, IEnumerable<Assembly> assemblies = null, Func<Type, Type> typeMapper = null)
    {
        var result = new AumlLoadResult();

        AumlDocument doc;
        try { doc = AumlParser.Parse(aumlText); }
        catch (Exception e) { result.Diagnostics.Add($"Parse error: {e.Message}"); return result; }

        if (doc.Root == null) { result.Diagnostics.Add("Empty / invalid AUML"); return result; }
        doc.RelativeFilePath ??= "preview.auml";
        doc.RootNamespace ??= "Adamantium.Designer.Preview";

        var asmList = (assemblies ?? AppDomain.CurrentDomain.GetAssemblies()).ToList();
        var resolver = new ReflectionTypeResolver(asmList);
        var diagnostics = new ListDiagnosticSink(result.Diagnostics);

        try { new DefaultAumlTransformer().Transform(doc, resolver, diagnostics); }
        catch (Exception e) { result.Diagnostics.Add($"Resolve error: {e.Message}"); }

        var instantiator = new AumlInstantiator(resolver, asmList, typeMapper, result.Diagnostics);
        try { result.Root = instantiator.Instantiate(doc.Root); }
        catch (Exception e) { result.Diagnostics.Add($"Instantiate error: {e.Message}"); }
        result.SourceMap = instantiator.SourceMap;   // for the designer's go-to-source / hover (may be partial on error)

        return result;
    }
}
