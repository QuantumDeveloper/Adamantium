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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var bindable = context.SyntaxProvider
            .ForAttributeWithMetadataName(BindableAttr, static (_, _) => true, static (ctx, ct) => GetBindable(ctx, ct))
            .Where(static m => m is not null);
        context.RegisterSourceOutput(bindable, static (spc, m) =>
        {
            if (!m.HasInpcBase)
            {
                spc.ReportDiagnostic(Diagnostic.Create(MissingInpc, Location.None, m.FieldName));
                return;
            }
            spc.AddSource(m.HintName, EmitBindable(m));
        });

        var command = context.SyntaxProvider
            .ForAttributeWithMetadataName(CommandAttr, static (_, _) => true, static (ctx, ct) => GetCommand(ctx, ct))
            .Where(static m => m is not null);
        context.RegisterSourceOutput(command, static (spc, m) => spc.AddSource(m.HintName, EmitCommand(m)));

        var viewModel = context.SyntaxProvider
            .ForAttributeWithMetadataName(ViewModelAttr, static (_, _) => true, static (ctx, ct) => GetViewModel(ctx, ct))
            .Where(static m => m is not null);
        context.RegisterSourceOutput(viewModel, static (spc, m) => spc.AddSource(m.HintName, EmitViewModel(m)));
    }

    // ---- transforms (run on the symbol; extract ONLY equatable primitives) -------------------------------------

    private static BindableMemberInfo GetBindable(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IFieldSymbol field) return null;
        var type = field.ContainingType;
        if (type is null || type.ContainingType is not null) return null;   // Phase 1: top-level types only

        var propName = ToPropertyName(field.Name);
        if (propName is null) return null;

        var commandNames = GetCommandNames(type);
        var affectsProperties = new List<string>();
        var affectsCommands = new List<string>();
        foreach (var attr in field.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != AffectsAttr) continue;
            foreach (var arg in attr.ConstructorArguments)
                foreach (var value in arg.Values)
                {
                    if (value.Value is not string s || s.Length == 0) continue;
                    (commandNames.Contains(s) ? affectsCommands : affectsProperties).Add(s);
                }
        }

        return new BindableMemberInfo(
            NamespaceOf(type),
            TypeKeyword(type),
            TypeNameWithGenerics(type),
            field.Name,
            propName,
            field.Type.ToDisplayString(FqnFormat),
            HasInpcHost(type),
            new EquatableArray<string>(affectsProperties.ToArray()),
            new EquatableArray<string>(affectsCommands.ToArray()),
            Hint(type, propName, "Bindable"));
    }

    private static CommandMemberInfo GetCommand(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;
        var type = method.ContainingType;
        if (type is null || type.ContainingType is not null) return null;

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

        return new CommandMemberInfo(
            NamespaceOf(type),
            TypeKeyword(type),
            TypeNameWithGenerics(type),
            commandName,
            "_" + commandName,
            commandTypeFqn,
            executeExpr,
            canExecuteExpr,
            Hint(type, commandName, "Command"));
    }

    private static ViewModelClassInfo GetViewModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
        if (type.ContainingType is not null) return null;                     // Phase 1: top-level
        if (ImplementsInpc(type)) return null;                                // already has INPC via a base → nothing to inject

        return new ViewModelClassInfo(
            NamespaceOf(type),
            TypeKeyword(type),
            TypeNameWithGenerics(type),
            Hint(type, "ViewModel", "INPC"));
    }

    // ---- emit ---------------------------------------------------------------------------------------------------

    private static SourceText EmitBindable(BindableMemberInfo m)
    {
        var sb = new StringBuilder();
        OpenType(sb, m.Namespace, m.TypeKeyword, m.TypeName);
        sb.AppendLine($"    public {m.PropertyType} {m.PropertyName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        get => {m.FieldName};");
        sb.AppendLine("        set");
        sb.AppendLine("        {");
        sb.AppendLine($"            On{m.PropertyName}Changing(value);");
        sb.AppendLine($"            if (SetProperty(ref {m.FieldName}, value))");
        sb.AppendLine("            {");
        sb.AppendLine($"                On{m.PropertyName}Changed(value);");
        foreach (var affected in m.AffectsProperties)
            sb.AppendLine($"                RaisePropertyChanged(\"{affected}\");");
        foreach (var command in m.AffectsCommands)
            sb.AppendLine($"                _{command}?.RaiseCanExecuteChanged();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    partial void On{m.PropertyName}Changing({m.PropertyType} value);");
        sb.AppendLine($"    partial void On{m.PropertyName}Changed({m.PropertyType} value);");
        CloseType(sb);
        return SourceText.From(sb.ToString(), Encoding.UTF8);
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

    // ---- symbol helpers ----------------------------------------------------------------------------------------

    private static string NamespaceOf(INamedTypeSymbol type) =>
        type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();

    private static string TypeKeyword(INamedTypeSymbol type) =>
        type.IsRecord ? (type.IsValueType ? "record struct" : "record") : type.IsValueType ? "struct" : "class";

    private static string TypeNameWithGenerics(INamedTypeSymbol type) =>
        type.TypeParameters.Length == 0 ? type.Name : $"{type.Name}<{string.Join(", ", type.TypeParameters.Select(p => p.Name))}>";

    private static bool HasInpcHost(INamedTypeSymbol type)
    {
        for (var b = type.BaseType; b is not null; b = b.BaseType)
            if (b.ToDisplayString() == PropertyChangedBaseName)
                return true;
        return type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ViewModelAttr);
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
