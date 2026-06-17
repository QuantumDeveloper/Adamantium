using System.Globalization;
using System.Reflection;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.MarkupExtension;
using Adamantium.UI.Markup.AST.TypeReference;
using Adamantium.UI.Markup.CodeGeneration;
using Adamantium.UI.Markup.CodeGeneration.Reflection;

namespace Adamantium.UI.Core.Markup;

/// <summary>
/// Walks a (already type-resolved) AUML AST and builds a live object tree by reflection: instantiate each
/// element, set its properties (text values, markup extensions, nested objects) and add children via
/// <see cref="IContainer.AddOrSetChildComponent"/>. Non-fatal issues are collected, not thrown, so a partly
/// invalid buffer (mid-edit) still previews what it can.
/// </summary>
internal sealed class AumlInstantiator
{
    private readonly ITypeResolver _resolver;
    private readonly List<Assembly> _assemblies;
    private readonly Func<Type, Type> _typeMapper;
    private readonly List<string> _diagnostics;

    public AumlInstantiator(ITypeResolver resolver, List<Assembly> assemblies, Func<Type, Type> typeMapper, List<string> diagnostics)
    {
        _resolver = resolver;
        _assemblies = assemblies;
        _typeMapper = typeMapper;
        _diagnostics = diagnostics;
    }

    public object Instantiate(AumlAstObjectNode node)
    {
        var clrType = ResolveClrType(node.TypeReference);
        if (clrType == null)
        {
            _diagnostics.Add($"Unknown type '{node.TypeReference?.GetFullTypeName()}'");
            return null;
        }

        var actualType = _typeMapper?.Invoke(clrType) ?? clrType;
        var instance = Activator.CreateInstance(actualType);

        foreach (var child in node.Children)
        {
            switch (child)
            {
                case AumlAstObjectNode objectNode:
                    var childObj = Instantiate(objectNode);
                    if (childObj is IAdamantiumComponent && instance is IContainer container)
                        container.AddOrSetChildComponent(childObj);
                    break;

                case AumlAstPropertyNode { Property: AumlAstPropertyReference pref } prop:
                    ApplyProperty(instance, pref, prop);
                    break;

                case AumlAstTextNode text when !string.IsNullOrWhiteSpace(text.Text):
                    if (instance is IContainer textHost) textHost.AddOrSetChildComponent(text.Text);
                    break;
            }
        }

        return instance;
    }

    private void ApplyProperty(object instance, AumlAstPropertyReference pref, AumlAstPropertyNode prop)
    {
        if (pref.IsAttachedProperty) return; // attached properties not supported in preview yet

        var p = instance.GetType().GetProperty(pref.Name, BindingFlags.Public | BindingFlags.Instance);
        if (p == null || !p.CanWrite)
        {
            _diagnostics.Add($"Property '{pref.Name}' not found / not writable on {instance.GetType().Name}");
            return;
        }

        foreach (var value in prop.Values)
        {
            switch (value)
            {
                case AumlAstMarkupExtensionNode markup:
                    var resolved = ResolveMarkupExtension(markup, p.PropertyType);
                    if (resolved != null) p.SetValue(instance, resolved);
                    break;

                case AumlAstTextNode textNode:
                    if (TryConvert(textNode.Text, p.PropertyType, out var converted))
                        p.SetValue(instance, converted);
                    break;

                case AumlAstObjectNode objectNode:
                    var nested = Instantiate(objectNode);
                    if (nested != null) p.SetValue(instance, nested);
                    break;
            }
        }
    }

    private object ResolveMarkupExtension(AumlAstMarkupExtensionNode markup, Type targetType)
    {
        var name = markup.TypeReference?.Name ?? string.Empty;

        // {ResourceReference Key} -> ResourceResolver.Resolve<targetType>(Key) (what the generator emits).
        if (name.StartsWith("ResourceReference"))
        {
            var key = (markup.Arguments.FirstOrDefault()?.Value as AumlAstTextNode)?.Text;
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                var method = typeof(ResourceResolver).GetMethod(nameof(ResourceResolver.Resolve))?.MakeGenericMethod(targetType);
                return method?.Invoke(null, new object[] { key });
            }
            catch { return null; }
        }

        _diagnostics.Add($"Markup extension '{name}' is not supported in the preview");
        return null;
    }

    private bool TryConvert(string text, Type targetType, out object result)
    {
        result = null;
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (t == typeof(string)) { result = text; return true; }
            if (t.IsEnum) { result = Enum.Parse(t, text, ignoreCase: true); return true; }
            if (t == typeof(bool)) { result = bool.Parse(text); return true; }
            if (t.IsPrimitive || t == typeof(decimal))
            {
                result = Convert.ChangeType(text, t, CultureInfo.InvariantCulture);
                return true;
            }

            if (typeof(Brush).IsAssignableFrom(t))
            {
                var field = typeof(Brushes).GetField(text, BindingFlags.Public | BindingFlags.Static);
                if (field != null) { result = field.GetValue(null); return true; }
            }

            var parse = t.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (parse != null) { result = parse.Invoke(null, new object[] { text }); return true; }

            // Last resort: the engine's TypeParser, which honours [TypeParser] attributes and the ParserRegistry.
            // This is exactly what the compiled code-behind generator emits (TypeParser.Parse<T>), so the live
            // preview converts the same value types a build does - e.g. a Path's SVG "Data" string into a Geometry
            // via GeometryParser (Geometry has no static Parse, so without this it stayed null and crashed the renderer).
            var typeParser = typeof(TypeParser).GetMethod(nameof(TypeParser.Parse))?.MakeGenericMethod(t);
            if (typeParser != null) { result = typeParser.Invoke(null, new object[] { text }); return true; }
        }
        catch { /* fall through */ }

        _diagnostics.Add($"Cannot convert '{text}' to {t.Name}");
        return false;
    }

    private Type ResolveClrType(IAumlAstTypeReference typeRef)
    {
        if (typeRef == null) return null;

        if (_resolver.Resolve(typeRef.GetFullTypeName()) is ReflectionResolvedType resolved)
            return resolved.ClrType;

        return _assemblies
            .SelectMany(ReflectionResolvedAssembly.SafeGetTypes)
            .FirstOrDefault(x => x.Name == typeRef.Name && x.Namespace == typeRef.Namespace);
    }
}
