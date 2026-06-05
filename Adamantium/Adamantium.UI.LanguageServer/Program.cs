using System.Runtime.InteropServices;
using Adamantium.UI.LanguageServer;
using Adamantium.UI.Markup.CodeGeneration;

const string xmlns = "http://adamantium/ui";

// 1. Locate the target project's output assemblies (the playground we built).
string? binDir = args.Length > 0 ? args[0] : FindDefaultBinDir();
if (binDir is null || !Directory.Exists(binDir))
{
    Console.Error.WriteLine($"Bin directory not found. Pass it as the first argument. Tried: {binDir}");
    return 1;
}
Console.WriteLine($"Loading assemblies from: {binDir}");

// App assemblies + the running runtime's assemblies (System.*); app wins on a name clash.
var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var dll in Directory.GetFiles(binDir, "*.dll"))
    byName[Path.GetFileName(dll)] = dll;
foreach (var dll in Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
    byName.TryAdd(Path.GetFileName(dll), dll);

var model = AumlTypeModel.Build(byName.Values);

// 2. Prove: element types are resolved under the AUML xmlns.
var elements = model.GetElements(xmlns);
Console.WriteLine($"\nElements under '{xmlns}': {elements.Count}");
foreach (var name in new[] { "Window", "Border", "RenderTargetPanel", "Grid", "TextBlock" })
    Console.WriteLine($"  [{(model.GetElement(xmlns, name) is not null ? "x" : " ")}] {name}");

// 3. Prove: properties of a concrete element, with their types and enum detection.
var border = model.GetElement(xmlns, "Border");
if (border is not null)
{
    var props = model.GetProperties(border);
    Console.WriteLine($"\n'Border' properties: {props.Count} (first 25, alphabetical)");
    foreach (var p in props.OrderBy(p => p.Name).Take(25))
    {
        var tag = p.PropertyType?.TypeKind == ResolvedTypeKind.Enum ? "  [enum]" : "";
        Console.WriteLine($"  {p.Name} : {p.PropertyType?.Name}{tag}");
    }

    // 4. Prove: enum value completion for the first enum-typed property.
    var enumProp = props.FirstOrDefault(p => p.PropertyType?.TypeKind == ResolvedTypeKind.Enum);
    if (enumProp is not null)
    {
        var values = model.GetEnumValues(enumProp.PropertyType).ToList();
        Console.WriteLine($"\nEnum values for '{enumProp.Name}' ({enumProp.PropertyType.Name}): {string.Join(", ", values)}");
    }
}

return 0;

static string? FindDefaultBinDir()
{
    var root = @"c:\AdamantiumEngine\Adamantium\output\Adamantium.UI.Playground\bin";
    if (!Directory.Exists(root)) return null;
    return Directory.EnumerateDirectories(root, "net*", SearchOption.AllDirectories)
        .Where(d => File.Exists(Path.Combine(d, "Adamantium.UI.dll")))
        .OrderByDescending(Directory.GetLastWriteTimeUtc)
        .FirstOrDefault();
}
