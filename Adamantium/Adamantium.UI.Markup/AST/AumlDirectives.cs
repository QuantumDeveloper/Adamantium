using System.Collections.Generic;

namespace Adamantium.UI.Markup.AST;

/// <summary>
/// Canonical registry of the AUML <c>x:</c> directives (xmlns <c>http://adamantium/ui/xaml/extensions</c>).
/// SINGLE SOURCE OF TRUTH: the transformer/code generator matches on the name constants, and the language server's
/// completion/hover derive their list from <see cref="All"/> - so adding a directive here surfaces it in tooling
/// without editing the language server. (Each directive's actual behaviour still lives in the transformer, since it
/// differs per directive; only the names + descriptions + the tooling list are consolidated here.)
/// </summary>
public static class AumlDirectives
{
    public const string Name = "Name";
    public const string Namespace = "Namespace";
    public const string Key = "Key";
    public const string Type = "Type";
    public const string ViewModel = "ViewModel";
    public const string CreateInDesignTime = "CreateInDesignTime";
    public const string Null = "Null";
    public const string Shared = "Shared";

    /// <summary>Every directive with the description tooling shows in completion and hover.</summary>
    public static readonly IReadOnlyList<AumlDirectiveInfo> All =
    [
        new AumlDirectiveInfo(Name, "Names this element so it is exposed as a field on the generated class."),
        new AumlDirectiveInfo(Namespace, "Full type name for the generated class (the WPF x:Class analog)."),
        new AumlDirectiveInfo(Key, "Key under which this entry is stored in a resource dictionary."),
        new AumlDirectiveInfo(Type, "A reference to a CLR type.", isTypeReference: true, usage: AumlDirectiveUsage.Value),
        new AumlDirectiveInfo(ViewModel, "View-model type for this view (prefix:Type). At runtime the framework resolves an instance from the DI container and assigns it as DataContext; design-time tooling resolves {Binding} paths against this type.", isTypeReference: true),
        new AumlDirectiveInfo(CreateInDesignTime, "Design-time only: \"True\" makes the preview instantiate x:ViewModel (parameterless ctor) so {Binding}s show real sample data. Off by default - the WPF d:IsDesignTimeCreatable behaviour."),
        new AumlDirectiveInfo(Null, "An explicit null value: Background=\"{x:Null}\".", usage: AumlDirectiveUsage.Value),
        new AumlDirectiveInfo(Shared, "\"False\" builds this value PER TARGET instead of sharing one instance between every element the setter matches - what a ContextMenu, a Popup or a Transform needs, since each belongs to the element it sits on."),
    ];

    /// <summary>The directive with this local name, or null if the name is not a directive at all.</summary>
    public static AumlDirectiveInfo Find(string name)
    {
        foreach (var directive in All)
        {
            if (directive.Name == name)
            {
                return directive;
            }
        }

        return null;
    }
}
