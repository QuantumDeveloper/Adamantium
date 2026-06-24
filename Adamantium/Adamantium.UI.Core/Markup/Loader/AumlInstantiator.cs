using System.Globalization;
using System.Reflection;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.MarkupExtensions;
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

    /// <summary>Each instantiated element mapped to its AUML source position (designer go-to-source / hover).
    /// Reference-keyed so controls that override Equals/GetHashCode don't collide.</summary>
    public Dictionary<object, AumlSourceSpan> SourceMap { get; } = new(ReferenceEqualityComparer.Instance);

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
        if (instance != null) SourceMap[instance] = new AumlSourceSpan(node.Line, node.Position);

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
        if (p == null)
        {
            _diagnostics.Add($"Property '{pref.Name}' not found on {instance.GetType().Name}");
            return;
        }

        // A collection-typed property-element (writable like <Button.Transitions>, or read-only like
        // <Theme.StyleIncludes>): its child ELEMENTS are added to the collection, never assigned - mirroring the
        // generator's IsCollection() path. Without this a writable collection (Transitions has a setter) takes the
        // assignment path below and fails ("DoubleTransition cannot be converted to Transitions").
        if (IsPopulatableCollection(p.PropertyType) && prop.Values.Any(v => v is AumlAstObjectNode))
        {
            PopulateCollection(instance, p, prop);
            return;
        }

        // A read-only collection property-element (e.g. <RenderTargetPanel.Behaviors>) whose declared type isn't a
        // concrete IList/ICollection is never assigned - its child elements are added to the existing collection instance.
        if (!p.CanWrite)
        {
            if (!TryAddToReadOnlyCollection(instance, p, prop))
                _diagnostics.Add($"Property '{pref.Name}' is not writable on {instance.GetType().Name}");
            return;
        }

        foreach (var value in prop.Values)
        {
            // A Binding/MultiBinding (markup-extension OR element syntax) sets up a live binding, not a plain value.
            if (instance is IFundamentalUIComponent bindable && TryBuildBindingBase(value, out var binding))
            {
                bindable.SetBinding(pref.Name, binding);
                continue;
            }

            // {ThemeResource Key}: a live link to the active theme's accent/focus property (not a data binding).
            if (instance is IFundamentalUIComponent themed &&
                value is AumlAstMarkupExtensionNode { TypeReference.Name: "ThemeResource" } trNode)
            {
                var key = (trNode.Arguments.FirstOrDefault()?.Value as AumlAstTextNode)?.Text?.Trim();
                if (!string.IsNullOrEmpty(key)) new ThemeResource(key).Apply(themed, pref.Name);
                continue;
            }

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

    /// <summary>A property whose declared type is a concrete collection (non-generic IList/ICollection, as
    /// <c>List&lt;T&gt;</c> is) and isn't parsed from text via [TypeParser] - mirrors the generator's IsCollection().</summary>
    private static bool IsPopulatableCollection(Type t) =>
        t != typeof(string)
        && (typeof(System.Collections.IList).IsAssignableFrom(t) || typeof(System.Collections.ICollection).IsAssignableFrom(t))
        && t.GetCustomAttribute<TypeParserAttribute>() == null;

    /// <summary>
    /// Populates a collection-typed property by ADDING each child element (mirrors the generator's IsCollection path):
    /// a settable collection (e.g. Transitions) gets a fresh instance, a read-only one (e.g. RowDefinitions) is added
    /// to in place. Per-child failures are reported individually so a partly-valid buffer still previews.
    /// </summary>
    private void PopulateCollection(object instance, PropertyInfo p, AumlAstPropertyNode prop)
    {
        object collection;
        if (p.CanWrite)
        {
            try { collection = Activator.CreateInstance(p.PropertyType); }
            catch { _diagnostics.Add($"Cannot create collection for '{p.Name}'"); return; }
            p.SetValue(instance, collection);
        }
        else
        {
            collection = p.GetValue(instance);
            if (collection == null) { _diagnostics.Add($"Collection '{p.Name}' is null on {instance.GetType().Name}"); return; }
        }

        var addMethods = collection.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Add" && m.GetParameters().Length == 1)
            .ToArray();

        foreach (var value in prop.Values)
        {
            if (value is not AumlAstObjectNode objectNode) continue;
            var child = Instantiate(objectNode);
            if (child == null) continue;

            var add = addMethods.FirstOrDefault(m => m.GetParameters()[0].ParameterType.IsInstanceOfType(child));
            if (add != null) add.Invoke(collection, [child]);
            else _diagnostics.Add($"Cannot add {child.GetType().Name} to {p.Name}");
        }
    }

    /// <summary>
    /// Populates a read-only collection property (no setter, e.g. Behaviors / Theme.StyleIncludes) by adding each
    /// child object element to the existing collection instance via its single-argument <c>Add</c>. Returns false
    /// when the property isn't a populatable collection (no instance, or no matching Add) so the caller can report it.
    /// </summary>
    private bool TryAddToReadOnlyCollection(object instance, PropertyInfo p, AumlAstPropertyNode prop)
    {
        var collection = p.GetValue(instance);
        if (collection == null) return false;

        var addMethods = collection.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Add" && m.GetParameters().Length == 1)
            .ToArray();
        if (addMethods.Length == 0) return false;

        foreach (var value in prop.Values)
        {
            if (value is not AumlAstObjectNode objectNode) continue;
            var child = Instantiate(objectNode);
            if (child == null) continue;

            var add = addMethods.FirstOrDefault(m => m.GetParameters()[0].ParameterType.IsInstanceOfType(child));
            if (add != null) add.Invoke(collection, [child]);
            else _diagnostics.Add($"Cannot add {child.GetType().Name} to {p.Name}");
        }
        return true;   // a populatable read-only collection; per-child issues are reported individually
    }

    // ---- bindings -------------------------------------------------------------------------------------------------

    // A Binding or MultiBinding written either as a markup extension ({Binding ...}/{MultiBinding ...}) or as an
    // element (<Binding .../>, <MultiBinding>...</MultiBinding>). Returns false for anything that isn't a binding.
    private bool TryBuildBindingBase(IAumlAstNode node, out BindingBase binding)
    {
        binding = node switch
        {
            AumlAstMarkupExtensionNode me => me.TypeReference?.Name switch
            {
                "Binding" => BuildBindingFromMarkup(me),
                "MultiBinding" => BuildMultiBindingFromMarkup(me),
                _ => null,
            },
            AumlAstObjectNode obj => obj.TypeReference?.Name switch
            {
                "Binding" => BuildBindingFromObject(obj),
                "MultiBinding" => BuildMultiBindingFromObject(obj),
                _ => null,
            },
            _ => null,
        };
        return binding != null;
    }

    private Binding BuildBindingFromMarkup(AumlAstMarkupExtensionNode me)
    {
        var binding = new Binding();
        foreach (var arg in me.Arguments)
        {
            if (string.IsNullOrEmpty(arg.Name))   // positional = Path ({Binding User.Name})
            {
                var path = (arg.Value as AumlAstTextNode)?.Text?.Trim();
                if (!string.IsNullOrEmpty(path)) binding.Path = new PropertyPath(path);
            }
            else ApplyBindingArg(binding, arg.Name, arg.Value);
        }
        return binding;
    }

    private Binding BuildBindingFromObject(AumlAstObjectNode obj)
    {
        var binding = new Binding();
        foreach (var child in obj.Children)
            if (child is AumlAstPropertyNode { Property: AumlAstPropertyReference pref } pn && pn.Values.Count > 0)
                ApplyBindingArg(binding, pref.Name, pn.Values[0]);
        return binding;
    }

    private MultiBinding BuildMultiBindingFromMarkup(AumlAstMarkupExtensionNode me)
    {
        var multi = new MultiBinding();
        foreach (var arg in me.Arguments)
        {
            if (string.IsNullOrEmpty(arg.Name))                       // positional = a child binding
            {
                if (TryBuildBindingBase(arg.Value, out var child)) multi.Bindings.Add(child);
            }
            else ApplyMultiBindingArg(multi, arg.Name, arg.Value);
        }
        return multi;
    }

    private MultiBinding BuildMultiBindingFromObject(AumlAstObjectNode obj)
    {
        var multi = new MultiBinding();
        foreach (var child in obj.Children)
        {
            switch (child)
            {
                case AumlAstObjectNode childObj when TryBuildBindingBase(childObj, out var nested):
                    multi.Bindings.Add(nested);                       // direct child <Binding/>/<MultiBinding/>
                    break;
                case AumlAstPropertyNode { Property: AumlAstPropertyReference pref } pn when pn.Values.Count > 0:
                    if (pref.Name == "Bindings")                       // explicit <MultiBinding.Bindings> element
                    {
                        foreach (var v in pn.Values)
                            if (TryBuildBindingBase(v, out var listed)) multi.Bindings.Add(listed);
                    }
                    else ApplyMultiBindingArg(multi, pref.Name, pn.Values[0]);
                    break;
            }
        }
        return multi;
    }

    private void ApplyBindingArg(Binding binding, string name, IAumlAstValueNode value)
    {
        switch (name)
        {
            case "Path": if ((value as AumlAstTextNode)?.Text is { } p) binding.Path = new PropertyPath(p.Trim()); break;
            case "Mode": if (TryEnum<BindingMode>(value, out var mode)) binding.Mode = mode; break;
            case "Converter": if (ResolveValue(value, typeof(IValueConverter)) is IValueConverter c) binding.Converter = c; break;
            case "ConverterParameter": binding.ConverterParameter = ResolveValue(value, typeof(object)); break;
            case "Source": binding.Source = ResolveValue(value, typeof(object)); break;
            case "StringFormat": binding.StringFormat = (value as AumlAstTextNode)?.Text; break;
            case "FallbackValue": binding.FallbackValue = ResolveValue(value, typeof(object)); break;
            case "TargetNullValue": binding.TargetNullValue = ResolveValue(value, typeof(object)); break;
            default: _diagnostics.Add($"Unknown Binding property '{name}'"); break;
        }
    }

    private void ApplyMultiBindingArg(MultiBinding multi, string name, IAumlAstValueNode value)
    {
        switch (name)
        {
            case "Mode": if (TryEnum<BindingMode>(value, out var mode)) multi.Mode = mode; break;
            case "Converter": if (ResolveValue(value, typeof(IMultiValueConverter)) is IMultiValueConverter c) multi.Converter = c; break;
            case "ConverterParameter": multi.ConverterParameter = ResolveValue(value, typeof(object)); break;
            case "StringFormat": multi.StringFormat = (value as AumlAstTextNode)?.Text; break;
            case "FallbackValue": multi.FallbackValue = ResolveValue(value, typeof(object)); break;
            case "TargetNullValue": multi.TargetNullValue = ResolveValue(value, typeof(object)); break;
            default: _diagnostics.Add($"Unknown MultiBinding property '{name}'"); break;
        }
    }

    private static bool TryEnum<T>(IAumlAstValueNode value, out T result) where T : struct
    {
        result = default;
        var text = (value as AumlAstTextNode)?.Text;
        return !string.IsNullOrEmpty(text) && Enum.TryParse(text, ignoreCase: true, out result);
    }

    // Resolves a markup value node to a runtime object: a markup extension (incl. a converter authored AS a markup
    // extension, or {ResourceReference}), an inline element (<...><local:MyConverter/></...>), or a text literal.
    private object ResolveValue(IAumlAstValueNode value, Type targetType) => value switch
    {
        AumlAstMarkupExtensionNode me => ResolveMarkupExtension(me, targetType),
        AumlAstObjectNode obj => Instantiate(obj),
        AumlAstTextNode text => TryConvert(text.Text, targetType, out var r) ? r : null,
        _ => null,
    };

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
                return method?.Invoke(null, [key]);
            }
            catch { return null; }
        }

        // General markup extension - e.g. a converter authored AS a MarkupExtension ({local:MyConverter}). Instantiate
        // it, set its named args, then call ProvideObject (a converter's ProvideObject typically returns itself).
        var clrType = ResolveClrType(markup.TypeReference);
        if (clrType == null)
        {
            _diagnostics.Add($"Markup extension '{name}' is not supported in the preview");
            return null;
        }

        object instance;
        try { instance = Activator.CreateInstance(clrType); }
        catch { _diagnostics.Add($"Cannot create markup extension '{name}'"); return null; }

        foreach (var arg in markup.Arguments)
        {
            if (string.IsNullOrEmpty(arg.Name)) continue;
            var prop = clrType.GetProperty(arg.Name, BindingFlags.Public | BindingFlags.Instance);
            if (prop is not { CanWrite: true }) continue;
            var argValue = ResolveValue(arg.Value, prop.PropertyType);
            if (argValue != null) prop.SetValue(instance, argValue);
        }

        return instance is MarkupExtension ext ? ext.ProvideObject(new MarkupContext()) : instance;
    }

    private bool TryConvert(string text, Type targetType, out object result)
    {
        result = null;
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (t == typeof(string)) { result = text; return true; }
            // An object-typed property (e.g. Setter.Value="Auto") takes the raw string in markup; the real
            // conversion happens later when the value is applied to its concrete target property.
            if (t == typeof(object)) { result = text; return true; }
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

            var parse = t.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, [typeof(string)], null);
            if (parse != null) { result = parse.Invoke(null, [text]); return true; }

            // Last resort: the engine's TypeParser, which honours [TypeParser] attributes and the ParserRegistry.
            // This is exactly what the compiled code-behind generator emits (TypeParser.Parse<T>), so the live
            // preview converts the same value types a build does - e.g. a Path's SVG "Data" string into a Geometry
            // via GeometryParser (Geometry has no static Parse, so without this it stayed null and crashed the renderer).
            result = TypeParser.Parse(text, t);
            return true;
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

    // ==== hot reload: live-tree reconciliation ====================================================================
    // Apply edited markup to an EXISTING object tree IN PLACE instead of rebuilding it: changed properties are
    // re-applied to the live instances (so a transition eases and animations elsewhere keep running) and added /
    // removed / replaced / reordered child elements are spliced into the live tree. <paramref name="oldNode"/> is the
    // AST the live tree was built from; <paramref name="newNode"/> the edited markup. The two are diffed - only real
    // changes touch the tree.

    public void Reconcile(object live, AumlAstObjectNode oldNode, AumlAstObjectNode newNode)
    {
        if (live == null || oldNode == null || newNode == null) return;
        ReconcileProperties(live, oldNode, newNode);
        ReconcileChildren(live, oldNode, newNode);
    }

    private void ReconcileProperties(object live, AumlAstObjectNode oldNode, AumlAstObjectNode newNode)
    {
        var oldProps = PropertyNodes(oldNode);
        var newProps = PropertyNodes(newNode);

        // Removed (present in the old markup, gone in the new): clear back to default.
        foreach (var name in oldProps.Keys)
            if (!newProps.ContainsKey(name)) ResetProperty(live, name);

        // Added / changed: re-apply ONLY when the value actually changed, so unchanged properties don't re-fire their
        // transitions (re-applying every property on each edit is exactly what made "the whole tree restart").
        foreach (var (name, newProp) in newProps)
        {
            if (oldProps.TryGetValue(name, out var oldProp) && NodeSignature(oldProp) == NodeSignature(newProp))
                continue;
            ApplyPropertyNode(live, newProp);
        }
    }

    private void ReconcileChildren(object live, AumlAstObjectNode oldNode, AumlAstObjectNode newNode)
    {
        if (live is not IContainer container) return;

        var oldKids = ObjectChildren(oldNode);
        var newKids = ObjectChildren(newNode);
        var liveKids = container.GetChildComponents();

        // The live children were built from oldKids in order. If that no longer lines up (e.g. a container that didn't
        // expose its children), we can't splice precisely - rebuild just this container's content.
        if (liveKids.Count != oldKids.Count)
        {
            RebuildChildren(container, newKids);
            return;
        }

        // Key each old child to its live instance; a new child with the same key reuses (and reconciles) it.
        var oldKeys = ChildKeys(oldKids);
        var newKeys = ChildKeys(newKids);
        var byKey = new Dictionary<string, (AumlAstObjectNode Node, object Instance)>(StringComparer.Ordinal);
        for (var i = 0; i < oldKids.Count; i++)
            byKey[oldKeys[i]] = (oldKids[i], liveKids[i]);

        // The desired final child instances, in the NEW order: reuse+reconcile matches, instantiate the rest.
        var desired = new List<object>(newKids.Count);
        for (var j = 0; j < newKids.Count; j++)
        {
            if (byKey.TryGetValue(newKeys[j], out var match))
            {
                Reconcile(match.Instance, match.Node, newKids[j]);
                desired.Add(match.Instance);
            }
            else
            {
                var created = Instantiate(newKids[j]);
                if (created != null) desired.Add(created);
            }
        }

        ApplyChildOrder(container, desired);
    }

    // Splices the container's children to match `desired` (a mix of reused live instances and freshly built ones),
    // preserving reused instances (so their animations keep running) and inserting/removing/reordering minimally.
    private static void ApplyChildOrder(IContainer container, List<object> desired)
    {
        // Drop live children no longer wanted (back-to-front to keep indices valid).
        var current = container.GetChildComponents();
        for (var i = current.Count - 1; i >= 0; i--)
            if (!desired.Any(d => ReferenceEquals(d, current[i])))
                container.RemoveChildComponentAt(i);

        // Position each desired child, moving reused instances and inserting new ones.
        for (var i = 0; i < desired.Count; i++)
        {
            current = container.GetChildComponents();
            if (i < current.Count && ReferenceEquals(current[i], desired[i])) continue;

            var existing = -1;
            for (var k = 0; k < current.Count; k++)
                if (ReferenceEquals(current[k], desired[i])) { existing = k; break; }
            if (existing >= 0) container.RemoveChildComponentAt(existing);
            container.InsertChildComponent(i, desired[i]);
        }
    }

    private void RebuildChildren(IContainer container, List<AumlAstObjectNode> newKids)
    {
        container.RemoveAllChildComponents();
        foreach (var kid in newKids)
        {
            var created = Instantiate(kid);
            if (created is IAdamantiumComponent) container.AddOrSetChildComponent(created);
        }
    }

    private void ApplyPropertyNode(object instance, AumlAstPropertyNode prop)
    {
        if (prop.Property is AumlAstPropertyReference pref) ApplyProperty(instance, pref, prop);
    }

    private static void ResetProperty(object instance, string name)
    {
        if (instance is not AdamantiumComponent comp) return;
        var p = comp.GetProperty(name);
        if (p != null) comp.ClearValue(p);   // clears the Local value -> back to default / style
    }

    private static Dictionary<string, AumlAstPropertyNode> PropertyNodes(AumlAstObjectNode node)
    {
        var result = new Dictionary<string, AumlAstPropertyNode>(StringComparer.Ordinal);
        foreach (var child in node.Children)
            if (child is AumlAstPropertyNode { Property: AumlAstPropertyReference pref } prop && !pref.IsAttachedProperty)
                result[pref.Name] = prop;
        return result;
    }

    private static List<AumlAstObjectNode> ObjectChildren(AumlAstObjectNode node)
    {
        var result = new List<AumlAstObjectNode>();
        foreach (var child in node.Children)
            if (child is AumlAstObjectNode obj) result.Add(obj);
        return result;
    }

    // A stable identity for each child: x:Name (or a Name property) when present, else type + occurrence index among
    // same-typed siblings. Lets reorders/renames reuse the same live instance instead of rebuilding it.
    private static List<string> ChildKeys(List<AumlAstObjectNode> kids)
    {
        var keys = new List<string>(kids.Count);
        var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kid in kids)
        {
            var type = kid.TypeReference?.Name ?? "?";
            var name = NameKeyOf(kid);
            if (!string.IsNullOrEmpty(name)) { keys.Add(type + "#" + name); continue; }
            var n = typeCounts.TryGetValue(type, out var c) ? c : 0;
            typeCounts[type] = n + 1;
            keys.Add(type + "@" + n);
        }
        return keys;
    }

    private static string NameKeyOf(AumlAstObjectNode node)
    {
        foreach (var child in node.Children)
        {
            if (child is AumlAstDirective { Name: "Name" } dir) return (dir.Value as AumlAstTextNode)?.Text;
            if (child is AumlAstPropertyNode { Property: AumlAstPropertyReference { Name: "Name" } } p)
                return (p.Values.FirstOrDefault() as AumlAstTextNode)?.Text;
        }
        return null;
    }

    // A textual signature of an AST node so old vs new property values can be compared for "did it actually change".
    private static string NodeSignature(IAumlAstNode node) => node switch
    {
        AumlAstPropertyNode p => "P:" + (p.Property is AumlAstPropertyReference r ? r.Name : "") + "=" +
                                 string.Join("|", p.Values.Select(NodeSignature)),
        AumlAstTextNode t => "T:" + t.Text,
        AumlAstMarkupExtensionNode m => "M:" + (m.TypeReference?.Name ?? "") + "(" +
                                        string.Join(",", m.Arguments.Select(a => a.Name + "=" + NodeSignature(a.Value))) + ")",
        AumlAstObjectNode o => "O:" + (o.TypeReference?.Name ?? "") + "{" +
                               string.Join(";", o.Children.Select(NodeSignature)) + "}",
        AumlAstDirective d => "D:" + d.Name + "=" + NodeSignature(d.Value),
        _ => node?.ToString() ?? ""
    };
}
