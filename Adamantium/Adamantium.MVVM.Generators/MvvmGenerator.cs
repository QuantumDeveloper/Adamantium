using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Adamantium.MVVM.Generators;

/// <summary>
/// The Adamantium MVVM incremental source generator: turns <c>[Bindable]</c> fields into observable properties,
/// <c>[Command]</c> methods into <c>ICommand</c> properties, and injects INPC into <c>[ViewModel]</c> classes.
/// Built for performance at 10k+ usages: one provider per attribute via <c>ForAttributeWithMetadataName</c> (the
/// fast attribute-indexed path), tiny value-equatable models out of every transform (no symbols/syntax carried
/// downstream), and one cached output file per member — so a keystroke regenerates only what actually changed.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MvvmGenerator : IIncrementalGenerator
{
    private const string BindableAttr = "Adamantium.MVVM.BindableAttribute";
    private const string AffectsAttr = "Adamantium.MVVM.AffectsAttribute";
    private const string CommandAttr = "Adamantium.MVVM.CommandAttribute";
    private const string ViewModelAttr = "Adamantium.MVVM.ViewModelAttribute";
    private const string PropertyChangedBaseName = "Adamantium.Core.PropertyChangedBase";
    private const string InpcName = "System.ComponentModel.INotifyPropertyChanged";

    private static readonly SymbolDisplayFormat FqnFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly DiagnosticDescriptor MissingInpc = new(
        "AMVVM001",
        "[Bindable] needs an INPC host",
        "Field '{0}' uses [Bindable] but its class neither derives from AdamantiumViewModel nor is marked [ViewModel]; no property was generated",
        "Adamantium.MVVM",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NestedTypeNotSupported = new(
        "AMVVM002",
        "MVVM types must be top-level",
        "Type '{0}' uses an MVVM attribute ([Bindable]/[Command]/[ViewModel]) but is nested; move it to the top level (nested types are not supported)",
        "Adamantium.MVVM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ValidationNeedsValidatingBase = new(
        "AMVVM003",
        "Validation needs AdamantiumValidatingViewModel",
        "Member '{0}' has validation attributes, but its class does not derive from AdamantiumValidatingViewModel; no validation will run",
        "Adamantium.MVVM",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private const string ValidatingBaseName = "Adamantium.MVVM.AdamantiumValidatingViewModel";
    private const string ValidationAttributeName = "System.ComponentModel.DataAnnotations.ValidationAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var bindable = context.SyntaxProvider
            .ForAttributeWithMetadataName(BindableAttr, static (_, _) => true, static (ctx, ct) => GetBindable(ctx, ct))
            .Where(static r => r is not null);
        context.RegisterSourceOutput(bindable, static (spc, r) =>
        {
            if (Report(spc, r)) return;
            var m = r.Value;
            if (!m.HasInpcBase)
            {
                spc.ReportDiagnostic(Diagnostic.Create(MissingInpc, Location.None, m.FieldName));
                return;
            }
            if (m.Warning is not null) spc.ReportDiagnostic(m.Warning.ToDiagnostic());   // e.g. AMVVM003; still emit
            spc.AddSource(m.HintName, EmitBindable(m));
        });

        var command = context.SyntaxProvider
            .ForAttributeWithMetadataName(CommandAttr, static (_, _) => true, static (ctx, ct) => GetCommand(ctx, ct))
            .Where(static r => r is not null);
        context.RegisterSourceOutput(command, static (spc, r) =>
        {
            if (Report(spc, r)) return;
            spc.AddSource(r.Value.HintName, EmitCommand(r.Value));
        });

        var viewModel = context.SyntaxProvider
            .ForAttributeWithMetadataName(ViewModelAttr, static (_, _) => true, static (ctx, ct) => GetViewModel(ctx, ct))
            .Where(static r => r is not null);
        context.RegisterSourceOutput(viewModel, static (spc, r) =>
        {
            if (Report(spc, r)) return;
            spc.AddSource(r.Value.HintName, EmitViewModel(r.Value));
        });
    }

    // ---- transforms (run on the symbol; extract ONLY equatable primitives) -------------------------------------

    private static Result<BindableMemberInfo> GetBindable(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        INamedTypeSymbol type;
        ISymbol member;
        string fieldName, propName, propType;
        bool isPartialProperty;

        switch (ctx.TargetSymbol)
        {
            case IFieldSymbol field:
                type = field.ContainingType;
                if (type is null) return null;
                if (type.ContainingType is not null) return Nested<BindableMemberInfo>(type);
                propName = ToPropertyName(field.Name);
                if (propName is null) return null;
                member = field;
                fieldName = field.Name;
                propType = field.Type.ToDisplayString(FqnFormat);
                isPartialProperty = false;
                break;

            case IPropertySymbol property:
                type = property.ContainingType;
                if (type is null) return null;
                if (type.ContainingType is not null) return Nested<BindableMemberInfo>(type);
                if (!property.IsPartialDefinition || property.GetMethod is null || property.SetMethod is null) return null;
                member = property;
                fieldName = "";
                propName = property.Name;
                propType = property.Type.ToDisplayString(FqnFormat);
                isPartialProperty = true;
                break;

            default:
                return null;
        }

        var commandNames = GetCommandNames(type);
        var affectsProperties = new List<string>();
        var affectsCommands = new List<string>();
        var validationAttributes = new List<string>();
        var hasValidationAttributes = false;
        var validates = DerivesFrom(type, ValidatingBaseName);
        foreach (var attr in member.GetAttributes())
        {
            var attrName = attr.AttributeClass?.ToDisplayString();
            if (attrName == AffectsAttr)
            {
                foreach (var arg in attr.ConstructorArguments)
                    foreach (var value in arg.Values)
                    {
                        if (value.Value is not string s || s.Length == 0) continue;
                        (commandNames.Contains(s) ? affectsCommands : affectsProperties).Add(s);
                    }
            }
            else if (attr.AttributeClass is not null && IsValidationAttribute(attr.AttributeClass))
            {
                hasValidationAttributes = true;
                // Re-emit onto the property only for the field path; a partial property already carries them.
                if (validates && !isPartialProperty) validationAttributes.Add(ReconstructAttribute(attr));
            }
        }

        // Validation attributes without a validating base would silently do nothing — warn, but still emit the property.
        var warning = hasValidationAttributes && !validates
            ? new DiagnosticInfo(ValidationNeedsValidatingBase, LocationInfo.CreateFrom(member), member.Name)
            : null;

        return new(new BindableMemberInfo(
            NamespaceOf(type),
            TypeKeyword(type),
            TypeNameWithGenerics(type),
            fieldName,
            propName,
            propType,
            HasInpcHost(type),
            isPartialProperty,
            hasValidationAttributes && validates,
            new EquatableArray<string>(affectsProperties.ToArray()),
            new EquatableArray<string>(affectsCommands.ToArray()),
            new EquatableArray<string>(validationAttributes.ToArray()),
            Hint(type, propName, "Bindable"),
            warning), null);
    }

    private static Result<CommandMemberInfo> GetCommand(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;
        var type = method.ContainingType;
        if (type is null) return null;
        if (type.ContainingType is not null) return Nested<CommandMemberInfo>(type);

        var isAsync = IsTaskReturning(method.ReturnType);
        var isVoid = method.ReturnType.SpecialType == SpecialType.System_Void;
        if (!isAsync && !isVoid) return null;                                 // only void or Task(-returning)

        // A single optional trailing CancellationToken (async only) is the command's token, not a parameter;
        // everything else counts as the typed parameter. We support at most one typed parameter.
        var parameters = method.Parameters;
        var hasCt = isAsync && parameters.Length > 0 && IsCancellationToken(parameters[parameters.Length - 1].Type);
        var significant = hasCt ? parameters.Length - 1 : parameters.Length;
        if (significant > 1) return null;                                     // multi-parameter commands unsupported
        var hasParam = significant == 1;
        if (hasParam && IsCancellationToken(parameters[0].Type)) return null; // a lone token is the token, not a parameter

        var paramTypeFqn = hasParam ? parameters[0].Type.ToDisplayString(FqnFormat) : null;

        string customName = null, canExecute = null;
        var attr = ctx.Attributes[0];
        foreach (var na in attr.NamedArguments)
        {
            if (na.Key == "Name") customName = na.Value.Value as string;
            else if (na.Key == "CanExecute") canExecute = na.Value.Value as string;
        }

        var commandName = string.IsNullOrEmpty(customName) ? method.Name + "Command" : customName;
        var commandTypeFqn = (isAsync, hasParam) switch
        {
            (false, false) => "global::Adamantium.MVVM.AdamantiumCommand",
            (false, true) => $"global::Adamantium.MVVM.AdamantiumCommand<{paramTypeFqn}>",
            (true, false) => "global::Adamantium.MVVM.AdamantiumAsyncCommand",
            (true, true) => $"global::Adamantium.MVVM.AdamantiumAsyncCommand<{paramTypeFqn}>",
        };
        var executeExpr = (isAsync, hasParam, hasCt) switch
        {
            (false, false, _) => $"() => {method.Name}()",
            (false, true, _) => $"arg => {method.Name}(arg)",
            (true, false, false) => $"_ => {method.Name}()",
            (true, false, true) => $"ct => {method.Name}(ct)",
            (true, true, false) => $"(arg, _) => {method.Name}(arg)",
            (true, true, true) => $"(arg, ct) => {method.Name}(arg, ct)",
        };

        var canExecuteExpr = BuildCanExecute(type, canExecute, hasParam);

        return new(new CommandMemberInfo(
            NamespaceOf(type),
            TypeKeyword(type),
            TypeNameWithGenerics(type),
            commandName,
            "_" + commandName,
            commandTypeFqn,
            executeExpr,
            canExecuteExpr,
            Hint(type, commandName, "Command")), null);
    }

    private static Result<ViewModelClassInfo> GetViewModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
        if (type.ContainingType is not null) return Nested<ViewModelClassInfo>(type);
        if (ImplementsInpc(type)) return null;                                // already has INPC via a base → nothing to inject

        return new(new ViewModelClassInfo(
            NamespaceOf(type),
            TypeKeyword(type),
            TypeNameWithGenerics(type),
            Hint(type, "ViewModel", "INPC")), null);
    }

    // ---- emit ---------------------------------------------------------------------------------------------------

    private static SourceText EmitBindable(BindableMemberInfo m)
    {
        var sb = new StringBuilder();
        OpenType(sb, m.Namespace, m.TypeKeyword, m.TypeName);
        foreach (var attribute in m.ValidationAttributes)            // field path only; empty for partial properties
            sb.AppendLine($"    [{attribute}]");

        if (m.IsPartialProperty) EmitPartialProperty(sb, m);
        else EmitFieldProperty(sb, m);

        sb.AppendLine();
        sb.AppendLine($"    partial void On{m.PropertyName}Changing({m.PropertyType} value);");
        sb.AppendLine($"    partial void On{m.PropertyName}Changed({m.PropertyType} value);");
        CloseType(sb);
        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }

    // Field-backed property: wraps the user's field with SetProperty (raises PropertyChanged for us).
    private static void EmitFieldProperty(StringBuilder sb, BindableMemberInfo m)
    {
        sb.AppendLine($"    public {m.PropertyType} {m.PropertyName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        get => {m.FieldName};");
        sb.AppendLine("        set");
        sb.AppendLine("        {");
        sb.AppendLine($"            On{m.PropertyName}Changing(value);");
        sb.AppendLine($"            if (SetProperty(ref {m.FieldName}, value))");
        sb.AppendLine("            {");
        sb.AppendLine($"                On{m.PropertyName}Changed(value);");
        EmitSetBody(sb, m, "                ");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    // Partial property: implements the user's `public partial T Name { get; set; }` using the `field` keyword,
    // so there is no separately declared backing field.
    private static void EmitPartialProperty(StringBuilder sb, BindableMemberInfo m)
    {
        sb.AppendLine($"    public partial {m.PropertyType} {m.PropertyName}");
        sb.AppendLine("    {");
        sb.AppendLine("        get => field;");
        sb.AppendLine("        set");
        sb.AppendLine("        {");
        sb.AppendLine($"            On{m.PropertyName}Changing(value);");
        sb.AppendLine($"            if (!global::System.Collections.Generic.EqualityComparer<{m.PropertyType}>.Default.Equals(field, value))");
        sb.AppendLine("            {");
        sb.AppendLine("                field = value;");
        sb.AppendLine($"                RaisePropertyChanged(\"{m.PropertyName}\");");
        sb.AppendLine($"                On{m.PropertyName}Changed(value);");
        EmitSetBody(sb, m, "                ");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    // Shared tail of the setter's changed-branch: validation + dependent property/command notifications.
    private static void EmitSetBody(StringBuilder sb, BindableMemberInfo m, string indent)
    {
        if (m.Validates)
            sb.AppendLine($"{indent}ValidateProperty(value, \"{m.PropertyName}\");");
        foreach (var affected in m.AffectsProperties)
            sb.AppendLine($"{indent}RaisePropertyChanged(\"{affected}\");");
        foreach (var command in m.AffectsCommands)
            sb.AppendLine($"{indent}_{command}?.RaiseCanExecuteChanged();");
    }

    private static SourceText EmitCommand(CommandMemberInfo m)
    {
        var canExec = m.CanExecuteExpression is null ? "" : ", " + m.CanExecuteExpression;
        var sb = new StringBuilder();
        OpenType(sb, m.Namespace, m.TypeKeyword, m.TypeName);
        sb.AppendLine($"    private {m.CommandTypeFqn} {m.FieldName};");
        sb.AppendLine($"    public {m.CommandTypeFqn} {m.CommandName} =>");
        sb.AppendLine($"        {m.FieldName} ??= new {m.CommandTypeFqn}({m.ExecuteExpression}{canExec});");
        CloseType(sb);
        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }

    private static SourceText EmitViewModel(ViewModelClassInfo m)
    {
        var sb = new StringBuilder();
        OpenType(sb, m.Namespace, m.TypeKeyword, m.TypeName, " : global::System.ComponentModel.INotifyPropertyChanged");
        sb.AppendLine("    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");
        sb.AppendLine();
        sb.AppendLine("    protected void RaisePropertyChanged([global::System.Runtime.CompilerServices.CallerMemberName] string propertyName = \"\")");
        sb.AppendLine("        => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));");
        sb.AppendLine();
        sb.AppendLine("    protected bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string propertyName = \"\")");
        sb.AppendLine("    {");
        sb.AppendLine("        if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;");
        sb.AppendLine("        field = value;");
        sb.AppendLine("        RaisePropertyChanged(propertyName);");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        CloseType(sb);
        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }

    private static void OpenType(StringBuilder sb, string ns, string keyword, string typeName, string bases = "")
    {
        // No "#nullable enable": the generated file inherits the consuming project's nullable setting, so it
        // matches the user's code (their fields and our output agree) instead of forcing a context on them.
        sb.AppendLine("// <auto-generated/>");
        if (ns.Length > 0)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }
        sb.AppendLine($"partial {keyword} {typeName}{bases}");
        sb.AppendLine("{");
    }

    private static void CloseType(StringBuilder sb) => sb.AppendLine("}");

    // A nested-type result: no emit, just the AMVVM002 error (located on the type, so duplicates across members of
    // the same type collapse to one). T is the would-be emit model.
    private static Result<T> Nested<T>(INamedTypeSymbol type) where T : class =>
        new(null, new DiagnosticInfo(NestedTypeNotSupported, LocationInfo.CreateFrom(type), type.Name));

    // Reports a transform's diagnostic if it carries one; returns true when the caller should stop (no emit).
    private static bool Report<T>(SourceProductionContext spc, Result<T> result) where T : class
    {
        if (result.Diagnostic is null) return false;
        spc.ReportDiagnostic(result.Diagnostic.ToDiagnostic());
        return true;
    }

    // ---- symbol helpers ----------------------------------------------------------------------------------------

    private static string NamespaceOf(INamedTypeSymbol type) =>
        type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();

    private static string TypeKeyword(INamedTypeSymbol type) =>
        type.IsRecord ? (type.IsValueType ? "record struct" : "record") : type.IsValueType ? "struct" : "class";

    private static string TypeNameWithGenerics(INamedTypeSymbol type) =>
        type.TypeParameters.Length == 0 ? type.Name : $"{type.Name}<{string.Join(", ", type.TypeParameters.Select(p => p.Name))}>";

    private static bool HasInpcHost(INamedTypeSymbol type) =>
        DerivesFrom(type, PropertyChangedBaseName) ||
        type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ViewModelAttr);

    private static bool DerivesFrom(INamedTypeSymbol type, string baseFqn)
    {
        for (var b = type.BaseType; b is not null; b = b.BaseType)
            if (b.ToDisplayString() == baseFqn)
                return true;
        return false;
    }

    private static bool IsValidationAttribute(INamedTypeSymbol attributeClass)
    {
        for (var t = attributeClass; t is not null; t = t.BaseType)
            if (t.ToDisplayString() == ValidationAttributeName)
                return true;
        return false;
    }

    // Reconstructs an attribute as fully-qualified C# text to re-emit on the generated property. TypedConstant's
    // ToCSharpString() renders literals/enums/typeof/arrays correctly and fully-qualified, so the result is valid
    // regardless of the using-directives in the user's file (which the generated file doesn't inherit).
    private static string ReconstructAttribute(AttributeData attribute)
    {
        var sb = new StringBuilder(attribute.AttributeClass.ToDisplayString(FqnFormat));
        var args = new List<string>();
        foreach (var ca in attribute.ConstructorArguments)
            args.Add(ca.ToCSharpString());
        foreach (var na in attribute.NamedArguments)
            args.Add($"{na.Key} = {na.Value.ToCSharpString()}");
        if (args.Count > 0)
            sb.Append('(').Append(string.Join(", ", args)).Append(')');
        return sb.ToString();
    }

    private static bool ImplementsInpc(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(i => i.ToDisplayString() == InpcName);

    // Task or Task<T> (not ValueTask — async commands need a started Task to await; ValueTask isn't required yet).
    private static bool IsTaskReturning(ITypeSymbol type)
    {
        if (type is null) return false;
        if (type.ToDisplayString() == "System.Threading.Tasks.Task") return true;
        return type is INamedTypeSymbol named && named.IsGenericType &&
               named.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.Task<TResult>";
    }

    private static bool IsCancellationToken(ITypeSymbol type) =>
        type?.ToDisplayString() == "System.Threading.CancellationToken";

    // Builds the CanExecute delegate argument. A typed command needs a Func<T,bool>: pass the arg through if the
    // user's CanExecute takes one, else ignore it ('_'); a non-typed command needs a parameterless Func<bool>.
    private static string BuildCanExecute(INamedTypeSymbol type, string canExecute, bool hasParam)
    {
        if (string.IsNullOrEmpty(canExecute)) return null;
        var member = type.GetMembers(canExecute).FirstOrDefault();
        var isProperty = member is IPropertySymbol;
        var takesArg = member is IMethodSymbol m && m.Parameters.Length == 1;
        if (hasParam)
        {
            if (isProperty) return $"_ => {canExecute}";
            return takesArg ? $"arg => {canExecute}(arg)" : $"_ => {canExecute}()";
        }
        return isProperty ? $"() => {canExecute}" : $"() => {canExecute}()";
    }

    // Generated command property names of the type, so [Affects] can tell a command from a property.
    private static HashSet<string> GetCommandNames(INamedTypeSymbol type)
    {
        var names = new HashSet<string>();
        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol method) continue;
            foreach (var attr in method.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != CommandAttr) continue;
                string custom = null;
                foreach (var na in attr.NamedArguments)
                    if (na.Key == "Name") custom = na.Value.Value as string;
                names.Add(string.IsNullOrEmpty(custom) ? method.Name + "Command" : custom);
            }
        }
        return names;
    }

    // _title / m_title / title -> Title; null if no distinct property name can be derived.
    private static string ToPropertyName(string fieldName)
    {
        var name = fieldName;
        if (name.StartsWith("m_")) name = name.Substring(2);
        else if (name.StartsWith("_")) name = name.Substring(1);
        name = name.TrimStart('_');
        if (name.Length == 0) return null;
        var prop = char.ToUpperInvariant(name[0]) + name.Substring(1);
        return prop == fieldName ? null : prop;
    }

    private static string Hint(INamedTypeSymbol type, string member, string kind)
    {
        var ns = NamespaceOf(type);
        var raw = (ns.Length > 0 ? ns + "." : "") + type.MetadataName + "." + member + "." + kind + ".g.cs";
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
            sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' ? c : '_');
        return sb.ToString();
    }
}
