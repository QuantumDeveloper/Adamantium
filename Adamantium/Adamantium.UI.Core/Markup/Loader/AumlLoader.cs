using System.Reflection;
using Adamantium.UI.Markup.AST;
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
        result.Ast = doc.Root;                       // kept so a later edit can be reconciled against it

        return result;
    }

    /// <summary>
    /// Hot reload: reconciles an existing live tree (<paramref name="liveRoot"/>, built from <paramref name="oldAst"/>)
    /// against edited markup IN PLACE - changed properties are re-applied to the live instances (transitions ease,
    /// other animations keep running) and added/removed/reordered children are spliced in, without rebuilding the tree.
    /// On success <see cref="AumlLoadResult.Reconciled"/> is true and <see cref="AumlLoadResult.Ast"/> is the new AST.
    /// It declines (Reconciled=false) when the root element type changed - the caller should then do a full rebuild.
    /// </summary>
    public static AumlLoadResult Reconcile(object liveRoot, AumlAstObjectNode oldAst, string newAumlText,
        IEnumerable<Assembly> assemblies = null, Func<Type, Type> typeMapper = null)
    {
        var result = new AumlLoadResult { Root = liveRoot };
        if (liveRoot == null || oldAst == null)
        {
            result.Diagnostics.Add("No live tree to reconcile");
            return result;
        }

        AumlDocument doc;
        try { doc = AumlParser.Parse(newAumlText); }
        catch (Exception e) { result.Diagnostics.Add($"Parse error: {e.Message}"); return result; }

        if (doc.Root == null) { result.Diagnostics.Add("Empty / invalid AUML"); return result; }
        doc.RelativeFilePath ??= "preview.auml";
        doc.RootNamespace ??= "Adamantium.Designer.Preview";

        // A changed root element type can't be patched in place - tell the caller to rebuild.
        if (!string.Equals(oldAst.TypeReference?.Name, doc.Root.TypeReference?.Name, StringComparison.Ordinal))
        {
            result.Ast = doc.Root;
            return result;   // Reconciled stays false
        }

        var asmList = (assemblies ?? AppDomain.CurrentDomain.GetAssemblies()).ToList();
        var resolver = new ReflectionTypeResolver(asmList);
        var diagnostics = new ListDiagnosticSink(result.Diagnostics);

        try { new DefaultAumlTransformer().Transform(doc, resolver, diagnostics); }
        catch (Exception e) { result.Diagnostics.Add($"Resolve error: {e.Message}"); }

        var instantiator = new AumlInstantiator(resolver, asmList, typeMapper, result.Diagnostics);
        try { instantiator.Reconcile(liveRoot, oldAst, doc.Root); result.Reconciled = true; }
        catch (Exception e) { result.Diagnostics.Add($"Reconcile error: {e.Message}"); }

        result.Ast = doc.Root;
        result.SourceMap = instantiator.SourceMap;   // spans for any newly-instantiated subtrees
        return result;
    }
}
