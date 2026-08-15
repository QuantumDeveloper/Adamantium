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
    public const string Static = "Static";
    public const string DataType = "DataType";
    public const string KeepAlive = "KeepAlive";
    public const string Load = "Load";

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
        new AumlDirectiveInfo(KeepAlive, "What this view asks of whoever navigates away from it: Disabled (default - rebuilt on every visit), Enabled (kept, but evictable) or Required (kept, never evicted). Metadata only - the view parks nothing itself."),
        new AumlDirectiveInfo(Load, "When this element is built at all. \"False\" holds it back until something asks for it by name; a binding (x:Load=\"{Binding IsAdvancedShown}\") builds it when the condition turns true and detaches it when it turns false. While unloaded NOTHING under it is constructed - it is not a hidden element, it is an absent one. Worth it for something HEAVY - a page, a list, a panel opened once a session; a small chunk costs more in slot than it saves in construction."),
        new AumlDirectiveInfo(DataType,"The type of the item a DataTemplate is written against (prefix:Type). Declared, not inferred: tooling resolves {Binding} paths inside the template against it, the way x:ViewModel does for a view. The type must exist - a name that does not resolve fails the build.", isTypeReference: true),
        new AumlDirectiveInfo(Static,"The value of a static field or property: Width=\"{x:Static local:Metrics.RailWidth}\". Read at the point of use, so the declaration stays in one place instead of being restated as a resource.", isTypeReference: true, usage: AumlDirectiveUsage.Value),
        new AumlDirectiveInfo(Shared,"\"False\" builds this value PER TARGET instead of sharing one instance between every element the setter matches - what a ContextMenu, a Popup or a Transform needs, since each belongs to the element it sits on."),
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
