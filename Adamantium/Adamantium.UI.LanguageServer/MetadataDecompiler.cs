using System.Text.RegularExpressions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Go-to-definition fallback for types that live only in metadata (an external assembly with no source):
/// decompiles the containing type with ICSharpCode.Decompiler into a cached temp .cs file and returns the line
/// of the requested member. Cache key includes the dll's write time, so a rebuilt assembly re-decompiles.
/// </summary>
public static class MetadataDecompiler
{
    private static readonly Dictionary<string, string> FileCache = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public static DefinitionLocation? Locate(ISymbol symbol, Compilation compilation)
    {
        var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        if (type is null) return null;

        var assembly = type.ContainingAssembly;
        if (assembly is null ||
            compilation.GetMetadataReference(assembly) is not PortableExecutableReference { FilePath: { Length: > 0 } dll })
            return null;

        var reflectionName = ReflectionName(type);
        string code, file;
        try { file = DecompileToFile(dll, reflectionName, out code); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[auml] decompile failed for {reflectionName} in {dll}: {ex.Message}");
            return null;
        }

        // Best-effort: land on the declaration line of the member (or the type itself when navigating a type).
        var memberName = symbol is INamedTypeSymbol ? type.Name : symbol.Name;
        int line = FindMemberLine(code, memberName);
        return new DefinitionLocation(file, line, 0, line, 0);
    }

    private static string DecompileToFile(string dll, string reflectionName, out string code)
    {
        var stamp = File.GetLastWriteTimeUtc(dll).Ticks;
        var key = $"{dll}|{reflectionName}|{stamp}";
        lock (Gate)
        {
            if (FileCache.TryGetValue(key, out var cached) && File.Exists(cached))
            {
                code = File.ReadAllText(cached);
                return cached;
            }
        }

        var decompiler = new CSharpDecompiler(dll, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
        code = decompiler.DecompileTypeAsString(new ICSharpCode.Decompiler.TypeSystem.FullTypeName(reflectionName));

        var dir = Path.Combine(Path.GetTempPath(), "adamantium-auml-decompiled",
            Path.GetFileNameWithoutExtension(dll) + "-" + stamp);
        Directory.CreateDirectory(dir);

        var simpleName = reflectionName.Replace('+', '.');
        int lastDot = simpleName.LastIndexOf('.');
        var fileName = StripArity(lastDot >= 0 ? simpleName[(lastDot + 1)..] : simpleName) + ".cs";
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, code);

        lock (Gate) FileCache[key] = path;
        return path;
    }

    // Reflection name ILSpy expects: "Namespace.Outer+Inner`arity" (MetadataName already carries the arity tick).
    private static string ReflectionName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
            name = outer.MetadataName + "+" + name;
        var ns = type.ContainingNamespace;
        return ns is { IsGlobalNamespace: false } ? ns.ToDisplayString() + "." + name : name;
    }

    private static string StripArity(string name)
    {
        int tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    private static int FindMemberLine(string code, string memberName)
    {
        var lines = code.Split('\n');
        var word = new Regex($@"\b{Regex.Escape(memberName)}\b");
        for (int i = 0; i < lines.Length; i++)
            if (word.IsMatch(lines[i])) return i;
        return 0;
    }
}
